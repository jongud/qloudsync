using System;
using GreenQloud.Model;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;
using GreenQloud.Util;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace GreenQloud
{
    public class AbortedOperationException : Exception
	{
        public AbortedOperationException(string cause) : base(cause){
           
        }
	}
}

