namespace RetailMonolith.Models
{
    public class AzureOpenAIConfiguration
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EmbeddingDeployment { get; set; } = string.Empty;
    }

    public class AzureSearchConfiguration
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
    }
}
