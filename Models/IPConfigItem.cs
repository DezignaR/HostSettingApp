using System;
using System.Collections.Generic;
using System.Text;

namespace WpfApp1.Models
{
    public class IPConfigItem
    {
        public string IPAddress { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string AddressFamily { get; set; } = "IPv4";

    }
}
