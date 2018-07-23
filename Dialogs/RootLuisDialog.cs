namespace LuisBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;

    [LuisModel("493b6434-b844-4487-8d38-09d1319673f2", "6c18e9d8d7164409b9ffefba1a431416")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        // private const string EntityGeographyCity = "builtin.geography.city";

        // private const string EntityHotelName = "Hotel";

        // private const string EntityAirportCode = "AirportCode";

        // private IList<string> titleOptions = new List<string> { "“Very stylish, great stay, great staff”", "“good hotel awful meals”", "“Need more attention to little things”", "“Lovely small hotel ideally situated to explore the area.”", "“Positive surprise”", "“Beautiful suite and resort”" };

        private static int sCurrentDialogID = 0;
        private static Dictionary<int, string> sDialogIdToCodeMap = new Dictionary<int, string>();
        private static string sLoginUrl = @"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=9cd99965-da20-4039-8e68-510d35bee7e2&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A3979%2Fapi%2FOAuthCallBack&response_mode=query&scope=offline_access%20user.read%20mail.read%20mail.send&state={0}";
        private int DialogId;

        internal static void UpdateCode(string code)
        {
            sDialogIdToCodeMap[sCurrentDialogID] = code;
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

        internal string Code
        {
            get
            {
                if (sDialogIdToCodeMap.ContainsKey(this.DialogId))
                {
                    return sDialogIdToCodeMap[this.DialogId];
                }

                return "";
            }
        }

        internal RootLuisDialog()
        {
            this.DialogId = ++sCurrentDialogID;
        }

        private bool CheckSignin(IDialogContext context, LuisResult result)
        {
            if (!IsSignedIn)
            {
                var card = SigninCard.Create("Please Sign In To Continue...", "Sign In", String.Format(sLoginUrl, DialogId));

                var resultMessage = context.MakeMessage();
                resultMessage.Attachments.Add(card.ToAttachment());
                context.PostAsync(resultMessage);
                return false;
            }

            return true;
        }


        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            if (!CheckSignin(context, result))
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
            if (!CheckSignin(context, result))
            {
                return;
            }

            var message = await activity;
            await context.PostAsync($"Searching your mail for '{message.Text}'...");
            //var hotelsQuery = new HotelsQuery();

            //EntityRecommendation cityEntityRecommendation;

            //if (result.TryFindEntity(EntityGeographyCity, out cityEntityRecommendation))
            //{
            //    cityEntityRecommendation.Type = "Destination";
            //}

            //var hotelsFormDialog = new FormDialog<HotelsQuery>(hotelsQuery, this.BuildHotelsForm, FormOptions.PromptInStart, result.Entities);

            //context.Call(hotelsFormDialog, this.ResumeAfterHotelsFormDialog);
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
            if (!CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Confirming the option.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.Reject")]
        public async Task Reject(IDialogContext context, LuisResult result)
        {
            if (!CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Ok rejecting the option.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.SendEmail")]
        public async Task SendEmail(IDialogContext context, LuisResult result)
        {
            if (!CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Send Email To:");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Communication.StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            if (!CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Type 'help' if you need assistance.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            if (!CheckSignin(context, result))
            {
                return;
            }

            await context.PostAsync("Hello! Try asking me things like 'search email sent by Rahul yesterday' or 'find mail I sent to the team last week'");

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
