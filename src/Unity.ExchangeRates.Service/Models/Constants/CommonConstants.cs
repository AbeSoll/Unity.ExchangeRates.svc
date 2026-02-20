namespace Unity.ExchangeRates.Service.Models.Constants
{
    public class CommonConstants
    {
        public struct StandardFormat
        {
            public static System.Globalization.CultureInfo Culture = new System.Globalization.CultureInfo("en-MY", false);
            public const string DateTime = "{0:dd/MM/yyyy}";
            public const string ISODateTime = "yyyy-MM-dd";
            public const string HashDateTime = "yyyyMMdd";
            public const string FailedStatus = "Failed";
        }

        public class ResponseMessage
        {
            public const string GENERAL_ERROR = "An error has occurred.";
            public const string VALIDATOR_ERROR = "One or more validation errors occurred.";
            public const string NOTFOUND_ERROR = "No record or record not found.";
            public const string DUPLICATED_ERROR = "Record already exists.";
        }
    }
}
