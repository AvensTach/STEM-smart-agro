using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAgro.Frontend.Helpers
{
    public static class ApiConfig
    {
#if DEBUG
       
        public static string BaseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5297"
            : "http://localhost:5297";
#else
    public static string BaseUrl = "https://tvoy-realniy-server.com";
#endif
    }
}
