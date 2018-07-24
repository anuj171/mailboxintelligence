namespace LuisBot.Dialogs
{
    using System;
    using System.Collections.Generic;
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

        // private IList<string> titleOptions = new List<string> { "“Very stylish, great stay, great staff”", "“good hotel awful meals”", "“Need more attention to little things”", "“Lovely small hotel ideally situated to explore the area.”", "“Positive surprise”", "“Beautiful suite and resort”" };

        private static int sCurrentDialogID = 0;
        private static Dictionary<int, string> sDialogIdToCodeMap = new Dictionary<int, string>();
        private static string sLoginUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=9cd99965-da20-4039-8e68-510d35bee7e2&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A3979%2Fapi%2FOAuthCallBack&response_mode=query&scope=offline_access%20user.read%20mail.read%20mail.send&state={0}";
        private static string sPostBody = "grant_type=authorization_code&client_id=9cd99965-da20-4039-8e68-510d35bee7e2&code={0}&redirect_uri=http%3A%2F%2Flocalhost%3A3979%2Fapi%2FOAuthCallBack&resource=https%3A%2F%2Fgraph.microsoft.com%2F&client_secret={1}";
        private static string sPostUrl = "https://login.microsoftonline.com/common/oauth2/token";
        private static string sClientSecret = "nlgKLCM17536*%fonsCFT*#";

        private int DialogId;

        private GraphService Service =  new GraphService();

        internal static void UpdateCodeAsync(string code)
        {
            sDialogIdToCodeMap.Add(sCurrentDialogID, code);
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

        internal string Token { get; set; }

        internal RootLuisDialog()
        {
            this.DialogId = ++sCurrentDialogID;
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

                    string content = String.Format(sPostBody, this.Code, Uri.EscapeDataString(sClientSecret));

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
                var card = SigninCard.Create("Please Sign In To Continue...", "Sign In", String.Format(sLoginUrl, DialogId));

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
            if (!await CheckSignin(context, result))
            {
                return;
            }

            string message = $"Sorry, I did not understand '{result.Query}'. Type 'help' if you need assistance.";

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.FindEmail")]
        public async Task Search(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
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
            }
            else
            {
                reply = $"Searching for: '{query.ToString()}'";
            }
             IList<Message> mails = Service.searchMails(Token, query);

            await context.PostAsync(reply);

            context.Wait(this.MessageReceived);
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
               // query.From = recommendation.Entity;
            }

            if (result.TryFindEntity(Email, out recommendation))
            {
                query.From = recommendation.Entity;
            }

            if (result.TryFindEntity(Email, out recommendation))
            {
                if (String.IsNullOrEmpty(query.From))
                {
                    query.From = recommendation.Entity;
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

        //[LuisIntent("ShowHotelsReviews")]
        //public async Task Reviews(IDialogContext context, LuisResult result)
        //{
        //    EntityRecommendation hotelEntityRecommendation;

        //    if (result.TryFindEntity(EntityHotelName, out hotelEntityRecommendation))
        //    {
        //        await context.PostAsync($"Looking for reviews of '{hotelEntityRecommendation.Entity}'...");

        //        var resultMessage = context.MakeMessage();
        //        resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //        resultMessage.Attachments = new List<Attachment>();

        //        for (int i = 0; i < 5; i++)
        //        {
        //            var random = new Random(i);
        //            ThumbnailCard thumbnailCard = new ThumbnailCard()
        //            {
        //                Title = this.titleOptions[random.Next(0, this.titleOptions.Count - 1)],
        //                Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Mauris odio magna, sodales vel ligula sit amet, vulputate vehicula velit. Nulla quis consectetur neque, sed commodo metus.",
        //                Images = new List<CardImage>()
        //                {
        //                    new CardImage() { Url = "https://upload.wikimedia.org/wikipedia/en/e/ee/Unknown-person.gif" }
        //                },
        //            };

        //            resultMessage.Attachments.Add(thumbnailCard.ToAttachment());
        //        }

        //        await context.PostAsync(resultMessage);
        //    }

        //    context.Wait(this.MessageReceived);
        //}

        [LuisIntent("Communication.Confirm")]
        public async Task Confirm(IDialogContext context, LuisResult result)
        {
            if (!await CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Confirming the option.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.Reject")]
        public async Task Reject(IDialogContext context, LuisResult result)
        {
            if (!await CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Ok rejecting the option.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.SendEmail")]
        public async Task SendEmail(IDialogContext context, LuisResult result)
        {
            if (!await CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Send Email To:");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            if (!await CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Type 'help' if you need assistance.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
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

            await context.PostAsync($"Hello {this.UserName}! Try asking me things like 'search email sent by Rahul yesterday' or 'find mail I sent to the team last week'");

            context.Wait(this.MessageReceived);
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
