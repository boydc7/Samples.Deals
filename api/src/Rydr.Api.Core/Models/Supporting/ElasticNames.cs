namespace Rydr.Api.Core.Models.Supporting
{
    public class ElasticIndexes
    {
        public const string DealsAlias = "deals";
        public const string CreatorsAlias = "creators";
        public const string MediaAlias = "medias";
        public const string BusinessesAlias = "businesses";
    }

    public static class ElasticHelpers
    {
        public static readonly char[] QueryStringReservedChars =
        {
            '+',
            '-',
            '=',
            '&',
            '|',
            '!',
            '(',
            ')',
            '{',
            '}',
            '[',
            ']',
            '^',
            '"',
            '~',
            '?',
            ':',
            '\\',
            '/',
            '*'
        };

        public static readonly char[] QueryStringForbiddenChars =
        {
            '>', '<'
        };
    }
}
