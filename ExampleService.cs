using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NateTorious.Durability
{
    public class ExampleService
    {
        public class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        public static async Task StaticTarget(Person person)
        {
            Console.WriteLine($"{nameof(StaticTarget)} - {person?.Name} / {person?.Age}");
            return;
        }

        public async Task InstanceTarget(Person person)
        {
            Console.WriteLine($"{nameof(InstanceTarget)} - {person?.Name} / {person?.Age}");
            return;
        }
    }
}
