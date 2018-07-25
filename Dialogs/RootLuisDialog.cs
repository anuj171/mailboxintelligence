namespace LuisBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Graph.Models;
    using Luis.Models;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Text.RegularExpressions;

    [LuisModel("493b6434-b844-4487-8d38-09d1319673f2", "6c18e9d8d7164409b9ffefba1a431416")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {

        // Entities
        private const string Content = "Content";
        private const string From = "From";
        private const string To = "To";
        private const string ToList = "ToList";
        private const string Users = "Users";
        private const string Date = "builtin.datetimeV2.date";
        private const string DateRange = "builtin.datetimeV2.daterange";
        private const string DateTime = "builtin.datetimeV2.datetime";
        private const string DateTimeRange = "builtin.datetimeV2.datetimerange";
        private const string Email = "builtin.email";

        private static Dictionary<string, string> sDialogIdToCodeMap = new Dictionary<string, string>();
        private static string sLoginUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={2}&response_type=code&redirect_uri={1}%2Fapi%2FOAuthCallBack&response_mode=query&scope=offline_access%20user.read%20mail.read%20mail.send%20people.read%20directory.read.all&state={0}";
        private static string sPostBody = "grant_type=authorization_code&client_id={3}&code={0}&redirect_uri={2}%2Fapi%2FOAuthCallBack&resource=https%3A%2F%2Fgraph.microsoft.com%2F&client_secret={1}";
        private static string sPostUrl = "https://login.microsoftonline.com/common/oauth2/token";

        private string Host = ConfigurationManager.AppSettings["Host"];
        private string ClientId = ConfigurationManager.AppSettings["MicrosoftAppId"];
        private string AppSecret = ConfigurationManager.AppSettings["MicrosoftAppPassword"];

        private string DialogId;

        private GraphService Service =  new GraphService();

        internal static void UpdateCodeAsync(string code, string dialogId)
        {
            if (sDialogIdToCodeMap.ContainsKey(dialogId))
            {
                sDialogIdToCodeMap.Remove(dialogId);
            }
            sDialogIdToCodeMap.Add(dialogId, code);
        }

        internal bool IsSignedIn {
            get
            {
                if (sDialogIdToCodeMap.ContainsKey(this.DialogId))
                {
                    return !String.IsNullOrEmpty(sDialogIdToCodeMap[this.DialogId]);
                }

                return false;
            }
        }

        internal string Code {
            get
            {
                return sDialogIdToCodeMap[this.DialogId];
            }
        }

        internal string UserName { get; set; }

        internal string UserManagerName { get; set; }

        internal string Token { get; set; }

        internal string ForwardMessageBody { get; set; }

        internal RootLuisDialog()
        {
            this.DialogId = Guid.NewGuid().ToString();
        }

        internal class AuthResponse
        {
            public string access_token { get; set; }
            public string token_type { get; set; }
        }

        public async Task<string> GetAuthToken(string accessToken)
        {
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, sPostUrl))
                {
                    request.Headers.Add("Host", "login.microsoftonline.com");

                    string content = String.Format(sPostBody, this.Code, Uri.EscapeDataString(this.AppSecret), Uri.EscapeDataString(this.Host), this.ClientId);

                    request.Content = new StringContent(content, Encoding.UTF8, "application/x-www-form-urlencoded");

                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string stringResult = await response.Content.ReadAsStringAsync();
                            AuthResponse obj = JsonConvert.DeserializeObject<AuthResponse>(stringResult);
                            return obj.access_token;
                        }
                        else return "";
                    }
                }
            }
        }

        private async Task<bool> CheckSignin(IDialogContext context, LuisResult result)
        {
            if (!IsSignedIn)
            {
                CardAction signInAction = new CardAction()
                {
                    Type = "openUrl",
                    Title = "Sign In",
                    Value = String.Format(sLoginUrl, DialogId, Uri.EscapeDataString(this.Host), this.ClientId)
                };

                HeroCard card = new HeroCard()
                {
                    Title = "Sign In",
                    Subtitle = "Tap/Click here to Sign In...",                    
                    Tap = signInAction
                };

                var resultMessage = context.MakeMessage();
                resultMessage.Attachments.Add(card.ToAttachment());
                await context.PostAsync(resultMessage);

                int counter = 20;
                while (!IsSignedIn && counter > 0)
                {
                    Thread.Sleep(2000);
                    --counter;
                }

                string message = $"Sign In Timed Out!";

                if (IsSignedIn)
                {
                    this.Token = await GetAuthToken(this.Code);

                    message = $"Signed In!";
                    await context.PostAsync(message);
                    return true;

                }

                await context.PostAsync(message);

                context.Wait(this.MessageReceived);

                return false;
            }

            return true;
        }


        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
                {
                    return;
                }

                string message = $"Sorry, I did not understand '{result.Query}'. Type 'help' if you need assistance.";

                await context.PostAsync(message);

                context.Wait(this.MessageReceived);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    await CheckSignin(context, result);
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("Communication.FindEmail")]
        public async Task Search(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
                {
                    return;
                }

                SearchQuery query = GetQueryFromResult(result);

                string reply;
                if (query.IsEmpty())
                {
                    reply = $"Sorry, I did not understand '{result.Query}'. Please be more specific.";
                    context.Wait(this.MessageReceived);
                }
                else
                {
                    reply = $"Searching for: '{query.ToString()}'";

                    await context.PostAsync(reply);

                    IList<Message> mails = new List<Message>();

                    mails = Service.searchMailsRestApi(Token, query);

                    await PublishCards(context, mails);
                }
                
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    if (await CheckSignin(context, result))
                    {
                        context.Wait(this.MessageReceived);
                    }
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }

        private static string HtmlToPlainText(string html)
        {
            const string tagWhiteSpace = @"(>|$)(\W|\n|\r)+<";//matches one or more (white space or line breaks) between '>' and '<'
            const string stripFormatting = @"<[^>]*(>|$)";//match any character between '<' and '>', even when end tag is missing
            const string lineBreak = @"<(br|BR)\s{0,1}\/{0,1}>";//matches: <br>,<br/>,<br />,<BR>,<BR/>,<BR />
            var lineBreakRegex = new Regex(lineBreak, RegexOptions.Multiline);
            var stripFormattingRegex = new Regex(stripFormatting, RegexOptions.Multiline);
            var tagWhiteSpaceRegex = new Regex(tagWhiteSpace, RegexOptions.Multiline);

            var text = html;
            //Decode html specific characters
            text = System.Net.WebUtility.HtmlDecode(text);
            //Remove tag whitespace/line breaks
            text = tagWhiteSpaceRegex.Replace(text, "><");
            //Replace <br /> with line breaks
            text = lineBreakRegex.Replace(text, Environment.NewLine);
            //Strip formatting
            text = stripFormattingRegex.Replace(text, string.Empty);

            return text;
        }

        public async Task PublishCards(IDialogContext context, IList<Message> msgs)
        {
            if(msgs.Count == 0)
            {
                await context.PostAsync("No Mail found");
                context.Wait(this.MessageReceived);
                return;
            }

            List<CardAction> Go = new List<CardAction>();
            var i = 0;
            foreach (var obj in msgs)
            {
                i++;
                string txt = HtmlToPlainText(obj.Body.Content.ToString());
                CardAction Actioncard = new CardAction()
                {
                    Type = ActionTypes.ImBack,
                    Title = obj.Subject,
                    Text = obj.Subject,
                    Value = txt

                };

                Go.Add(Actioncard);
                if(i == 5)
                {
                    //Displayin only 5 Email
                    break;
                }
            }

            HeroCard card = new HeroCard { Title = "Below Email found.", Buttons = Go };
            var message = context.MakeMessage();
            message.Attachments.Add(card.ToAttachment());
            await context.PostAsync(message);
            context.Wait(this.SelectedMail);
        }
        private bool ContainsHTML(string CheckString)
        {
            return Regex.IsMatch(CheckString, "<(.|\n)*?>");
        }

        protected async Task SelectedMail(IDialogContext context, IAwaitable<object> result)
        {
        try
            {
                var message = await result as IMessageActivity;
                if(!ContainsHTML(message.Text))
                {
                    await context.PostAsync("Did not found correct selection. Please try again! ");
                    context.Wait(this.MessageReceived);
                    return;
                }
                this.ForwardMessageBody = message.Text;

                await context.PostAsync("Whom you want to send selected mail ");
                context.Wait(this.MessageReceived);
            }
            catch (Exception e)
            {
                await context.PostAsync(e.Message);
                await context.PostAsync(e.StackTrace);
                context.Wait(this.MessageReceived);
            }
        }

        SearchQuery GetQueryFromResult(LuisResult result)
        {
            SearchQuery query = new SearchQuery();
            EntityRecommendation recommendation;
            if (result.TryFindEntity(Content, out recommendation))
            {
                query.Content = recommendation.Entity;
            }


            if (result.TryFindEntity(From, out recommendation))
            {
                query.From = recommendation.Entity;
                if (query.From.Contains("@"))
                {
                    query.From = query.From.Replace(" ", "");
                }
            }
                if (query.From.Contains("manager"))
                {
                    query.From = this.UserManagerName;
                }
            }

            if (result.TryFindEntity(Email, out recommendation))
            {
                if (String.IsNullOrEmpty(query.From))
                {
                    query.From = recommendation.Entity;
                    query.From = query.From.Replace(" ", "");
                }
            }

            if (result.TryFindEntity(To, out recommendation))
            {
                query.To = recommendation.Entity;

                if (query.To.Equals("me", StringComparison.InvariantCultureIgnoreCase) ||
                    query.To.Equals("myself", StringComparison.InvariantCultureIgnoreCase))
                {
                    query.To = this.UserName;
                }

                if (query.To.Contains("@"))
                {
                    query.To = query.To.Replace(" ", "");
                }
            }
                if (query.To.Contains("manager"))
                {
                    query.To = this.UserManagerName;
                }
            }

            if (result.TryFindEntity(Date, out recommendation) || result.TryFindEntity(DateTime, out recommendation))
            {
                string dateStr = "";
                EntityRecommendationExtensions.TryGetValue(recommendation, out dateStr);

                query.Date = dateStr;
            }

            if (result.TryFindEntity(DateRange, out recommendation) || result.TryFindEntity(DateTimeRange, out recommendation))
            {
                Range range;
                EntityRecommendationExtensions.TryGetRange(recommendation, out range);

                query.TimeStart = range.Start;
                query.TimeEnd = range.End;
            }

            return query;
        }

        [LuisIntent("Communication.Confirm")]
        public async Task Confirm(IDialogContext context, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
                {
                    return;
                }

                await context.PostAsync("Confirming the option.");

                context.Wait(this.MessageReceived);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    if (await CheckSignin(context, result))
                    {
                        context.Wait(this.MessageReceived);
                    }
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("Communication.Reject")]
        public async Task Reject(IDialogContext context, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
                {
                    return;
                }

                await context.PostAsync("Ok rejecting the option.");

                context.Wait(this.MessageReceived);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    if (await CheckSignin(context, result))
                    {
                        context.Wait(this.MessageReceived);
                    }
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }

        public class UserEmailAddress
        {
            [JsonProperty("@odata.etag")]
            public string etag { get; set; }
            public string userPrincipalName { get; set; }
            public string displayName { get; set; }
        }

        public class RootObject
        {
            [JsonProperty("@odata.context")]
            public string context { get; set; }
            public List<UserEmailAddress> value { get; set; }
        }

        [LuisIntent("Communication.SendEmail")]
        public async Task SendEmail(IDialogContext context, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
                {
                    return;
                }

                EntityRecommendation recommendation;             

                if (result.TryFindEntity(Email, out recommendation))
                {
                    String email;
                    email = recommendation.Entity;
                    email = email.Replace(" ", "");

                    //Valid Email Provided in cotext Send Mail to that mail
                    GraphService emailService = new GraphService();
                    MessageRequest emailMessageRequest = new MessageRequest();
                    emailMessageRequest = await emailService.BuildEmailMessage(Token, email, "Mail From Mailbox Intelligence Bot");

                    await context.PostAsync("Sending email to: " + email);

                    string resultMessage = await emailService.SendEmail(Token, emailMessageRequest);
                    await context.PostAsync(resultMessage);
                    context.Wait(this.MessageReceived);

                    return;
                }

                String to;
                if (result.TryFindEntity(To, out recommendation))
                {
                    to = recommendation.Entity;

                    if (to.Equals("me", StringComparison.InvariantCultureIgnoreCase) ||
                        to.Equals("myself", StringComparison.InvariantCultureIgnoreCase))
                    {
                        to = this.UserName;
                    }

                    if (to.Contains("@"))
                    {
                        to = to.Replace(" ", "");
                    }


                    var toekn = this.Token;
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", toekn);
                    var name = to;
                    var url = "https://graph.microsoft.com/beta/me/people?$search=" + name + "&$select=userPrincipalName,displayName";
                    var jsonResponse = await client.GetStringAsync(url);

                    var value = JsonConvert.DeserializeObject<RootObject>(jsonResponse);
                    List<UserEmailAddress> emailList = value.value;
                    if (emailList.Count == 0)
                    {
                        await context.PostAsync("No EMail ID found");
                        context.Wait(this.MessageReceived);
                        return;
                    } else if(emailList.Count == 1)
                    {
                        //Only one Email ID found send Email
                        await context.PostAsync("Found Email id: " + emailList[0].userPrincipalName);
                        await context.PostAsync("Sending Email To: " + emailList[0].userPrincipalName);

                        GraphService emailService = new GraphService();
                        MessageRequest emailMessageRequest = new MessageRequest();
                        emailMessageRequest = await emailService.BuildEmailMessage(Token, emailList[0].userPrincipalName, "Mail From Mailbox Intelligence Bot");
                        string resultMessage = await emailService.SendEmail(Token, emailMessageRequest);
                        await context.PostAsync(resultMessage);
                        context.Wait(this.MessageReceived);

                        return;
                    }
                    List<CardAction> Go = new List<CardAction>();
                    foreach (var obj in emailList)
                    {

                            CardAction Actioncard = new CardAction()
                            {
                                // Type =  ActionTypes.ImBack,
                                Title = obj.userPrincipalName,
                                Text = obj.displayName,
                                Value = obj.userPrincipalName

                            };
                            Go.Add(Actioncard);
                    }

                    HeroCard card = new HeroCard { Title = "Below Email ID found. Please choose whom to send Email", Buttons = Go };
                    var message = context.MakeMessage();
                    message.Attachments.Add(card.ToAttachment());
                    await context.PostAsync(message);
                    context.Wait(this.SendMailOnSelectedMail);
                }
                else
                {
                    await context.PostAsync("Sorry, I did not understand, please try again.");
                    context.Wait(this.MessageReceived);
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    if (await CheckSignin(context, result))
                    {
                        context.Wait(this.MessageReceived);
                    }
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }

        bool IsValidEmail(string strIn)
        {
            // Return true if strIn is in valid e-mail format.
            return Regex.IsMatch(strIn, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");
        }

        protected async Task SendMailOnSelectedMail(IDialogContext context, IAwaitable<object> result)
        {

            try
            { 
                var message = await result as IMessageActivity;

                if(String.IsNullOrEmpty(this.ForwardMessageBody) || !IsValidEmail(message.Text))
                {
                    await context.PostAsync("Did not found correct selection. Please try again! ");
                    context.Wait(this.MessageReceived);
                    //context.Done(new object());
                    return;
                }
                GraphService emailService = new GraphService();
                MessageRequest emailMessageRequest = new MessageRequest();
                if (String.IsNullOrEmpty(this.ForwardMessageBody))
                {
                    emailMessageRequest = await emailService.BuildEmailMessage(Token, message.Text, "Test Mail from bot app");
                }
                else
                {
                    emailMessageRequest = await emailService.BuildEmailMessageUsingBody(Token, message.Text, "Test Mail from bot app", this.ForwardMessageBody);
                    this.ForwardMessageBody = "";
                }

                await context.PostAsync("Sending email to: " + message.Text);
                string resultMessage = await emailService.SendEmail(Token, emailMessageRequest);
                await context.PostAsync(resultMessage);
                context.Wait(this.MessageReceived);
                
            }
            catch (Exception e)
            {
                await context.PostAsync(e.Message);
                await context.PostAsync(e.StackTrace);
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("Communication.StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            // clear credentials as we are starting over
            sDialogIdToCodeMap[this.DialogId] = null;
            if (await CheckSignin(context, result))
            {
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            try
            {
                if (!await CheckSignin(context, result))
            {
                return;
            }

                if (this.UserName == null)
                {
                    this.UserName = "";
                    this.UserName = await Service.GetMyName(this.Token);
                }
                if (this.UserManagerName == null)
                {
                    this.UserManagerName = "";
                    this.UserManagerName = await Service.GetMyManagerName(this.Token);
                }
                await context.PostAsync($"Hello {this.UserName}! Try asking me things like 'search email sent by Rahul yesterday' or 'find mail I sent to the team last week'");

                context.Wait(this.MessageReceived);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Expired") || e.Message.Contains("Auth"))
                {
                    await context.PostAsync("Your singin has expired, please signin and try again!");
                    sDialogIdToCodeMap[this.DialogId] = null;
                    if (await CheckSignin(context, result))
                    {
                        context.Wait(this.MessageReceived);
                    }
                }
                else
                {
                    await context.PostAsync(e.Message);
                    await context.PostAsync(e.StackTrace);
                }
                context.Wait(this.MessageReceived);
            }
        }


        //private IForm<HotelsQuery> BuildHotelsForm()
        //{
        //    OnCompletionAsyncDelegate<HotelsQuery> processHotelsSearch = async (context, state) =>
        //    {
        //        var message = "Searching for hotels";
        //        if (!string.IsNullOrEmpty(state.Destination))
        //        {
        //            message += $" in {state.Destination}...";
        //        }
        //        else if (!string.IsNullOrEmpty(state.AirportCode))
        //        {
        //            message += $" near {state.AirportCode.ToUpperInvariant()} airport...";
        //        }

        //        await context.PostAsync(message);
        //    };

        //    return new FormBuilder<HotelsQuery>()
        //        .Field(nameof(HotelsQuery.Destination), (state) => string.IsNullOrEmpty(state.AirportCode))
        //        .Field(nameof(HotelsQuery.AirportCode), (state) => string.IsNullOrEmpty(state.Destination))
        //        .OnCompletion(processHotelsSearch)
        //        .Build();
        //}

        //private async Task ResumeAfterHotelsFormDialog(IDialogContext context, IAwaitable<HotelsQuery> result)
        //{
        //    try
        //    {
        //        var searchQuery = await result;

        //        var hotels = await this.GetHotelsAsync(searchQuery);

        //        await context.PostAsync($"I found {hotels.Count()} hotels:");

        //        var resultMessage = context.MakeMessage();
        //        resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //        resultMessage.Attachments = new List<Attachment>();

        //        foreach (var hotel in hotels)
        //        {
        //            HeroCard heroCard = new HeroCard()
        //            {
        //                Title = hotel.Name,
        //                Subtitle = $"{hotel.Rating} starts. {hotel.NumberOfReviews} reviews. From ${hotel.PriceStarting} per night.",
        //                Images = new List<CardImage>()
        //                {
        //                    new CardImage() { Url = hotel.Image }
        //                },
        //                Buttons = new List<CardAction>()
        //                {
        //                    new CardAction()
        //                    {
        //                        Title = "More details",
        //                        Type = ActionTypes.OpenUrl,
        //                        Value = $"https://www.bing.com/search?q=hotels+in+" + HttpUtility.UrlEncode(hotel.Location)
        //                    }
        //                }
        //            };

        //            resultMessage.Attachments.Add(heroCard.ToAttachment());
        //        }

        //        await context.PostAsync(resultMessage);
        //    }
        //    catch (FormCanceledException ex)
        //    {
        //        string reply;

        //        if (ex.InnerException == null)
        //        {
        //            reply = "You have canceled the operation.";
        //        }
        //        else
        //        {
        //            reply = $"Oops! Something went wrong :( Technical Details: {ex.InnerException.Message}";
        //        }

        //        await context.PostAsync(reply);
        //    }
        //    finally
        //    {
        //        context.Done<object>(null);
        //    }
        //}

        //private async Task<IEnumerable<Hotel>> GetHotelsAsync(HotelsQuery searchQuery)
        //{
        //    var hotels = new List<Hotel>();

        //    // Filling the hotels results manually just for demo purposes
        //    for (int i = 1; i <= 5; i++)
        //    {
        //        var random = new Random(i);
        //        Hotel hotel = new Hotel()
        //        {
        //            Name = $"{searchQuery.Destination ?? searchQuery.AirportCode} Hotel {i}",
        //            Location = searchQuery.Destination ?? searchQuery.AirportCode,
        //            Rating = random.Next(1, 5),
        //            NumberOfReviews = random.Next(0, 5000),
        //            PriceStarting = random.Next(80, 450),
        //            Image = $"https://placeholdit.imgix.net/~text?txtsize=35&txt=Hotel+{i}&w=500&h=260"
        //        };

        //        hotels.Add(hotel);
        //    }

        //    hotels.Sort((h1, h2) => h1.PriceStarting.CompareTo(h2.PriceStarting));

        //    return hotels;
        //}
    }
}
