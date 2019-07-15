using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mappy.Core;

namespace Mappy.ConsoleTest
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var mapper = new TargetMapper();

            var result = mapper.Map(new SourceClass {Foo = "asd", Bar = 12, Grandpa = new SourceClass()}, new TargetClass());
            CheckMappings();
            Console.WriteLine("Hello World!");
            Console.WriteLine();
        }

        public static void CheckMappings()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var methods = assemblies
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods())
                .Where(m => m.GetCustomAttributes(typeof(WarnIfNotMappedCompletelyAttribute)).Any());

            Console.WriteLine(string.Join(",", methods.Select(m => m.Name)));
        }
    }


    public class SourceClass : BaseClass
    {
        public string Foo { get; set; }
        public int Bar { get; set; }
        public object Grandpa { get; set; }
    }

    public class BaseClass
    {
        public int Coolio { get; set; }
    }
    public class TargetClass : BaseClass
    {
        public string Foo { get; set; }
        public int Bar { get; set; }
        public object Grandpa { get; set; }
    }

    public class TargetMapper 
    {
        //[WarnIfNotMappedCompletely(nameof(TargetClass.Grandpa))]
        public TargetClass Map(SourceClass source, TargetClass target)
        {
            
        }

        [WarnIfNotMappedCompletely(nameof(SourceClass.Grandpa), nameof(SourceClass.Foo))]
        public abstract TargetClass MapingTest(SourceClass source, TargetClass target, int coool)
        {
            target.Bar = 122;
            target.Bar = 12345;
            target.Coolio = 000;
            target.Grandpa = new HashSet<string>();
            return target;
        }
    }
}