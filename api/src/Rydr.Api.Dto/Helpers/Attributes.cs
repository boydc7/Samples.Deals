using System;

namespace Rydr.Api.Dto.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DecorateWithContactNameAttribute : Attribute
    {
        public DecorateWithContactNameAttribute() { }

        public DecorateWithContactNameAttribute(string contactIdPropertyName)
        {
            ContactIdPropertyName = contactIdPropertyName;
        }

        public string ContactIdPropertyName { get; set; }
    }
}
