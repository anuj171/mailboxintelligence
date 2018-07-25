using LuisBot.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace LuisBot
{
    public class OAuthCallBackController : ApiController
    {
        [HttpGet]
        // GET api/<controller>
        public string Get()
        {
            var query = HttpUtility.ParseQueryString(this.Request.RequestUri.Query);
            string code = query.Get("code");
            string dialogId = query.Get("state");

            if (!String.IsNullOrEmpty(code))
            {
                RootLuisDialog.UpdateCodeAsync(code, dialogId);
                return "You are Signed In!";
            }

            return "Sign-in Failed!";
        }
    }
}