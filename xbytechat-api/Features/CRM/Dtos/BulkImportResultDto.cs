namespace xbytechat.api.Features.CRM.Dtos
{
    public class BulkImportResultDto
    {
        public int Imported { get; set; }          // brand-new rows inserted
        public int Restored { get; set; }          // previously IsActive=false -> restored
        public int SkippedExisting { get; set; }   // already active -> skipped
        public int DuplicatesInFile { get; set; }  // same phone repeated in CSV -> ignored
        public List<CsvImportError> Errors { get; set; } = new();
    }

    // Optional: remove this if unused (CsvImportError already exists elsewhere)
    public class CsvImportErrorMsg
    {
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; }
    }
}


//namespace xbytechat.api.Features.CRM.Dtos
//{
//    public class BulkImportResultDto
//    {
//        public int Imported { get; set; }
//        public int SkippedExisting { get; set; }
//        public List<CsvImportError> Errors { get; set; } = new();
//    }

//    public class CsvImportErrorMsg
//    {
//        public int RowNumber { get; set; }
//        public string ErrorMessage { get; set; }
//    }
//}