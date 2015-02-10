using System;

namespace VidyoIntegration.CommonLib.VidyoTypes.TransportClasses
{
    [Serializable]
    public class Room
    {
        private readonly DateTime _createdDateTime = DateTime.Now;
        private string _roomKey = "";

        public int RoomId { get; set; }

        public string RoomKey
        {
            get
            {
                // Parse room key if we need to
                if (string.IsNullOrEmpty(_roomKey))
                {
                    try
                    {
                        var parameters = System.Web.HttpUtility.ParseQueryString(RoomUrl);
                        _roomKey = parameters["key"];
                    }
                    catch (Exception ex)
                    {
                        Trace.Common.exception(ex);
                    }
                }

                // Return value
                return _roomKey;
            }
        }

        public string Name { get; set; }
        public string Extension { get; set; }
        public string Pin { get; set; }
        public string RoomUrl { get; set; }
        public DateTime CreatedDateTime { get { return _createdDateTime; } }
    }
}
