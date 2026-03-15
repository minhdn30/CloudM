using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.AuthDTOs
{
    public class PasswordStatusResponse
    {
        public bool HasPassword { get; set; }

        public IReadOnlyList<ExternalLoginSummaryResponse> ExternalLogins { get; set; }
            = Array.Empty<ExternalLoginSummaryResponse>();
    }
}
