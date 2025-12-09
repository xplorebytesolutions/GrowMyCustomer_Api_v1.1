using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CsvBatchValidationRequestDto
    {
        /// <summary>Explicit phone column to use; if null we'll try to auto-detect.</summary>
        public string? PhoneField { get; set; }

        /// <summary>Normalize phones (strip punctuation/leading zeros; add 91 for 10-digit local).</summary>
        public bool NormalizePhones { get; set; } = true;

        /// <summary>Report duplicates after normalization.</summary>
        public bool Deduplicate { get; set; } = true;

        /// <summary>Headers that must exist in the CSV.</summary>
        public List<string>? RequiredHeaders { get; set; }

        /// <summary>How many problematic rows to include in the response samples.</summary>
        public int SampleSize { get; set; } = 20;
    }

    public sealed class CsvBatchValidationResultDto
    {
        public Guid BatchId { get; set; }
        public int TotalRows { get; set; }

        public string? PhoneField { get; set; }

        public int MissingPhoneCount { get; set; }
        public int DuplicatePhoneCount { get; set; }

        public List<string> MissingRequiredHeaders { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        /// <summary>Sample of problematic rows (missing phone / dup / other).</summary>
        public List<CsvRowSampleDto> ProblemSamples { get; set; } = new();
    }
}
