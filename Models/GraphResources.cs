using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Graph.Models
{
    public class UserInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }

    }

    public class SearchQuery
    {
        public string Content { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string TimeStart { get; set; }
        public string TimeEnd { get; set; }

        public bool IsEmpty()
        {
            return String.IsNullOrEmpty(Content) &&
                String.IsNullOrEmpty(From) &&
                String.IsNullOrEmpty(To) &&
                String.IsNullOrEmpty(TimeStart) &&
                String.IsNullOrEmpty(TimeEnd);
        }

        public override string ToString()
        {
            string query = "";
            if (!String.IsNullOrEmpty(Content))
            {
                query += "QueryString: " + Content + "  ";
            }
            if (!String.IsNullOrEmpty(From))
            {
                query += "From: " + From + "  ";
            }
            if (!String.IsNullOrEmpty(To))
            {
                query += "To: " + To + "  ";
            }
            if (!String.IsNullOrEmpty(TimeStart))
            {
                query += "TimeStart: " + TimeStart + "  ";
            }
            if (!String.IsNullOrEmpty(TimeEnd))
            {
                query += "TimeEnd: " + TimeEnd + "  ";
            }

            return query;
        }
    }

    public class FileInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SharingLink { get; set; }
    }

    public class Message
    {
        public string Subject { get; set; }
        public ItemBody Body { get; set; }
        public List<Recipient> ToRecipients { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class Recipient
    {
        public UserInfo EmailAddress { get; set; }
    }

    public class ItemBody
    {
        public string ContentType { get; set; }
        public string Content { get; set; }
    }

    public class MessageRequest
    {
        public Message Message { get; set; }
        public bool SaveToSentItems { get; set; }
    }

    public class Attachment
    {
        [JsonProperty(PropertyName = "@odata.type")]
        public string ODataType { get; set; }
        public byte[] ContentBytes { get; set; }
        public string Name { get; set; }
    }

    public class PermissionInfo
    {
        public SharingLinkInfo Link { get; set; }
    }

    public class SharingLinkInfo
    {
        public SharingLinkInfo(string type)
        {
            Type = type;
        }

        public string Type { get; set; }
        public string WebUrl { get; set; }
    }
}