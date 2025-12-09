namespace xbytechat.api.Features.CRM.Dtos
{
    public class CsvImportResult<T>
    {
        public List<T> SuccessRecords { get; set; } = new();
        public List<CsvImportError> Errors { get; set; } = new();
    }

    public class CsvImportError
    {
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}