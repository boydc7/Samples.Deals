using System.Runtime.Serialization;

namespace Rydr.Api.Dto.Messages
{
    [DataContract]
    public class ApnPushNotification
    {
        [DataMember(Name = "APNS")]
        public string Apns { get; set; }

        [DataMember(Name = "APNS_SANDBOX")]
        public string ApnsSandbox { get; set; }
    }

    [DataContract]
    public class ApnPushNotificationFormat
    {
        [DataMember(Name = "aps")]
        public ApnPushNotificationAps Aps { get; set; }

        [DataMember(Name = "rydrObject")]
        public string RydrObject { get; set; }
    }

    [DataContract]
    public class ApnPushNotificationAps
    {
        [DataMember(Name = "alert")]
        public PushNotificationTitleBody Alert { get; set; }

        [DataMember(Name = "badge")]
        public int? Badge { get; set; }

        [DataMember(Name = "content-available")]
        public int? IsBackgroundUpdate { get; set; }

        [DataMember(Name = "sound")]
        public string Sound { get; set; }

        [DataMember(Name = "category")]
        public string Category { get; set; }
    }

    [DataContract]
    public class GcmPushNotification
    {
        [DataMember(Name = "GCM")]
        public string Gcm { get; set; }
    }

    [DataContract]
    public class GcmPushNotificationFormat
    {
        [DataMember(Name = "notification")]
        public PushNotificationTitleBody Notification { get; set; }

        [DataMember(Name = "data")]
        public GcmPushNotificationData Data { get; set; }
    }

    [DataContract]
    public class GcmPushNotificationData
    {
        [DataMember(Name = "notification")]
        public PushNotificationTitleBody Notification { get; set; }

        [DataMember(Name = "rydrObject")]
        public string RydrObject { get; set; }
    }

    [DataContract]
    public class PushNotificationTitleBody
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }
    }
}
