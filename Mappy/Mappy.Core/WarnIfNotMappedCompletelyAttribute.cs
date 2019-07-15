using System;
using System.Collections.Generic;
using System.Linq;

namespace Mappy.Core
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WarnIfNotMappedCompletelyAttribute : Attribute
    {
        public List<string> IgnoredTargetProperties { get; }

        public WarnIfNotMappedCompletelyAttribute(params string[] ignoredTargetProperties)
        {
            IgnoredTargetProperties = ignoredTargetProperties.ToList();
        }
    }
}