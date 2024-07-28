namespace Rydr.Api.Core.Models.Supporting;

[AttributeUsage(AttributeTargets.Property)]
public class PopulateExistingAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class PopulateAllExistingAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class IgnorePopulateExistingAttribute : Attribute { }
