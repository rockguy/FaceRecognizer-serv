using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TestServer.Models
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueValue:Attribute
    {

    }
}