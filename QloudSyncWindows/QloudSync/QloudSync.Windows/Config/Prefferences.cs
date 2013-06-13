using System;
using System.Configuration;

namespace GreenQloud
{
    public class Prefferences : Settings
    {
        public static bool NotificationsEnabled {
            get {
                return bool.Parse (ConfigurationManager.AppSettings ["NotificationsEnabled"]);
            }
            set
            {
                AppSettingsUpdate ("NotificationsEnabled", value.ToString());
            }
        }
    }
}

