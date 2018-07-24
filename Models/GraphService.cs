/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Graph;
using System.Linq;

namespace Graph.Models
{            
    
    // This sample shows how to:
    //    - Get the current user's email address
    //    - Get the current user's profile photo
    //    - Attach the photo as a file attachment to an email message
    //    - Upload the photo to the user's root drive
    //    - Get a sharing link for the file and add it to the message
    //    - Send the email
    [Serializable]
    public class GraphService
    {

        // Get the current user's email address from their profile.
        public async Task<string> GetMyName(string accessToken)
        {

            // Get the current user. 
            // The app only needs the user's email address, so select the mail and userPrincipalName properties.
            // If the mail property isn't defined, userPrincipalName should map to the email for all account types. 
            string endpoint = "https://graph.microsoft.com/v1.0/me";
            string queryParameter = "?$select=mail,userPrincipalName,displayName";
            UserInfo me = new UserInfo();

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint + queryParameter))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    // This header has been added to identify our sample in the Microsoft Graph service. If extracting this code for your project please remove.
                    //request.Headers.Add("SampleID", "aspnet-connect-rest-sample");

                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                            me.Name = !string.IsNullOrEmpty(json.GetValue("displayName").ToString()) ? json.GetValue("displayName").ToString() : json.GetValue("userPrincipalName").ToString();
                        }
                        return me.Name?.Trim();
                    }
                }
            }
        }

        // Get the current user's profile photo.
        public async Task<Stream> GetMyProfilePhoto(string accessToken)
        {

            // Get the profile photo of the current user (from the user's mailbox on Exchange Online). 
            // This operation in version 1.0 supports only a user's work or school mailboxes and not personal mailboxes. 
            string endpoint = "https://graph.microsoft.com/v1.0/me/photo/$value";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint))
                {
                    //request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await client.SendAsync(request);

                    // If successful, Microsoft Graph returns a 200 OK status code and the photo's binary data. If no photo exists, returns 404 Not Found.
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStreamAsync();
                    }
                    else
                    {
                        // If no photo exists, the sample uses a local file.
                        return System.IO.File.OpenRead(System.Web.Hosting.HostingEnvironment.MapPath("/Content/test.jpg"));
                    }
                }
            }
        }

        // Upload a file to OneDrive.
        // This call creates or updates the file.
        public async Task<FileInfo> UploadFile(string accessToken, Stream file)
        {

            // This operation only supports files up to 4MB in size.
            // To upload larger files, see `https://developer.microsoft.com/graph/docs/api-reference/v1.0/api/item_createUploadSession`.
            string endpoint = "https://graph.microsoft.com/v1.0/me/drive/root/children/mypic.jpg/content";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Put, endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Content = new StreamContent(file);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpg");
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string stringResult = await response.Content.ReadAsStringAsync();
                            return JsonConvert.DeserializeObject<FileInfo>(stringResult);
                        }
                        else return null;
                    }
                }
            }
        }

        // Create a sharing link for the file if one doesn't already exist.
        // See `https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/item_createlink`.
        public async Task<string> CreateSharingLinkForFile(string accessToken, FileInfo file)
        {
            string endpoint = $"https://graph.microsoft.com/v1.0/me/drive/items/{ file.Id }/createLink";
            SharingLinkInfo link = new SharingLinkInfo("view");

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    //request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    request.Content = new StringContent(JsonConvert.SerializeObject(link), Encoding.UTF8, "application/json");    
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string stringResult = await response.Content.ReadAsStringAsync();
                            PermissionInfo permission = JsonConvert.DeserializeObject<PermissionInfo>(stringResult);
                            return permission.Link.WebUrl; 
                        }
                        else return "";
                    }
                }
            }
        }

        // Send an email message from the current user.
        public async Task<string> SendEmail(string accessToken, MessageRequest email)
        {
            string endpoint = "https://graph.microsoft.com/v1.0/me/sendMail";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
             
                    request.Content = new StringContent(JsonConvert.SerializeObject(email), Encoding.UTF8, "application/json");
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return "Mail was sent successfully!";
                        }
                        return response.ReasonPhrase;
                    }
                }
            }
        }

        // Create the email message.
        public async Task<MessageRequest> BuildEmailMessage(string accessToken, string recipients, string subject)
        {

            // Prepare the recipient list.
            string[] splitter = { ";" };
            string[] splitRecipientsString = recipients.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
            List<Recipient> recipientList = new List<Recipient>();
            foreach (string recipient in splitRecipientsString)
            {
                recipientList.Add(new Recipient
                {
                    EmailAddress = new UserInfo
                    {
                        Address = recipient.Trim()
                    }
                });
            }

            // Get the current user's profile photo (or a test image if no profile photo exists).
            using (Stream photo = await GetMyProfilePhoto(accessToken))
            {
                // Add the photo as a file attachment for the email message.
                byte[] photoBytes = null;
                using (var memoryStream = new MemoryStream())
                {
                    photo?.CopyTo(memoryStream);
                    photoBytes = memoryStream.ToArray();
                }
                List<Attachment> attachments = new List<Attachment>();
                attachments.Add(new Attachment
                {
                    ODataType = "#microsoft.graph.fileAttachment",
                    ContentBytes = photoBytes,
                    Name = "mypic.jpg"
                });

                // Upload the photo to the user's root drive and then create a sharing link.
                FileInfo file = await UploadFile(accessToken, photo);
                file.SharingLink = await CreateSharingLinkForFile(accessToken, file);

                // Add the sharing link to the email body.
                string bodyContent = string.Format("" /* TODO Resource.Graph_SendMail_Body_Content*/, file.SharingLink);

                // Build the email message.
                Message message = new Message
                {
                    Body = new ItemBody
                    {
                        Content = bodyContent,
                        ContentType = "HTML"
                    },
                    Subject = subject,
                    ToRecipients = recipientList,
                    Attachments = attachments
                };

                return new MessageRequest
                {
                    Message = message,
                    SaveToSentItems = true
                };
            }
        }

        public List<Message> searchMails(string accessToken, SearchQuery query )
        {
            string queryString = string.Empty;
            if (String.IsNullOrEmpty(query.Date))
            {
                queryString = (String.IsNullOrEmpty(query.TimeStart) ? "receivedDateTime ge 2000-01-01" : "receivedDateTime ge " + query.TimeStart);
                queryString += (String.IsNullOrEmpty(query.TimeEnd) ? "" : " and receivedDateTime lt " + query.TimeEnd);

            }
            else
            {
                queryString += "receivedDateTime gt " + ((DateTime.Parse(query.Date)).AddDays(-1)).ToString("yyyy-MM-dd");
                queryString += " and receivedDateTime lt " +((DateTime.Parse(query.Date)).AddDays(1)).ToString("yyyy-MM-dd");
                //queryString = "receivedDateTime eq '" + query.Date + "'";
            }
            queryString += (String.IsNullOrEmpty(query.From) ? "" : " and from/emailAddress/address eq '" + query.From+ "'");
            queryString += (String.IsNullOrEmpty(query.Content) ? "" : " and subject eq '" + query.Content + "'");
            

            GraphServiceClient client = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    (requestMessage) =>
                    {
                        requestMessage.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", accessToken);

                        return Task.FromResult(0);
                    }));
            var mailResults = new List<Message>();
            try
            {
                var responseData =  client.Me.MailFolders.Inbox.Messages.Request()
                                    .OrderBy("receivedDateTime DESC")
                                    //  .Filter("from/emailAddress/address eq 'hack@microsoft.com' and receivedDateTime ge 2018-07-15 and receivedDateTime lt 2018-07-23 and search='Intelligent+Mailbox+Assistant'")
                                    .Filter(queryString)

                                    .Select("subject,receivedDateTime,from,body")
                                    .Top(5)
                                    .GetAsync().GetAwaiter().GetResult().CurrentPage.ToList<Microsoft.Graph.Message>();
                mailResults = responseData.Select(e => new Message { Subject = e.Subject, Body = new ItemBody { Content = e.Body.Content, ContentType = e.Body.ContentType.ToString()} }).ToList<Message>();
               
                // .Filter("startswith(displayName, 'hackathon') or (receivedDateTime ge 2018 - 07 - 15 and receivedDateTime lt 2018-07-23)")
                // https://graph.microsoft.com/v1.0/me/messages?$filter=from/emailAddress/address eq 'hack@microsoft.com'
                //https://graph.microsoft.com/v1.0/users?$filter=startswith(displayName,'hackathon') or receivedDateTime ge 2018-07-15 and receivedDateTime lt 2018-07-23 or startswith(surname,'mary') or startswith(mail,'mary') or startswith(userPrincipalName,'mary')

            }
            catch (Exception ex)
            {
                //TODO
                throw ex;
            }
            return mailResults;
        }

    }
}