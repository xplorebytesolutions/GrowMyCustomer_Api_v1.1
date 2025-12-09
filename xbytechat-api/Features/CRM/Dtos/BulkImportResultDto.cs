namespace xbytechat.api.Features.CRM.Dtos
{
    public class BulkImportResultDto
    {
        public int Imported { get; set; }
        public List<CsvImportError> Errors { get; set; } = new();
    }

    public class CsvImportErrorMsg
    {
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; }
    }
}