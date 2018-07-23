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

            if (!String.IsNullOrEmpty(code))
            {
                RootLuisDialog.UpdateCodeAsync(code);
                return "You are Signed In!";
            }

            return "Sign-in Failed!";
        }


        //// GET api/<controller>/5
        //public string Get(int id)
        //{
        //    return "value";
        //}

        // POST api/<controller>
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        //// PUT api/<controller>/5
        //public void Put(int id, [FromBody]string value)
        //{
        //}

        //// DELETE api/<controller>/5
        //public void Delete(int id)
        //{
        //}
    }
}