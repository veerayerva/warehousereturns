using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace JsonDeserializationTests
{
    public class ProcessingRequest
    {
        public string ListItemId { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
    }

    public class JsonDeserializationTest
    {
        private readonly ITestOutputHelper _output;

        public JsonDeserializationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestDefaultJsonDeserialization()
        {
            // Test the exact JSON that's failing
            string json = """
            {
              "listItemId": "21",
              "correlationId": "string"
            }
            """;

            _output.WriteLine($"Testing JSON: {json}");

            // Try default deserialization (should fail)
            var request = JsonSerializer.Deserialize<ProcessingRequest>(json);
            
            _output.WriteLine($"Default - ListItemId: '{request?.ListItemId}', CorrelationId: '{request?.CorrelationId}'");
            
            Assert.NotNull(request);
            // This will likely fail with default settings
            Assert.True(string.IsNullOrEmpty(request.ListItemId), "Default deserialization should fail due to case sensitivity");
        }

        [Fact]
        public void TestCaseInsensitiveJsonDeserialization()
        {
            // Test the exact JSON that's failing
            string json = """
            {
              "listItemId": "21",
              "correlationId": "string"
            }
            """;

            _output.WriteLine($"Testing JSON: {json}");

            // Try case-insensitive deserialization (should work)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var request = JsonSerializer.Deserialize<ProcessingRequest>(json, jsonOptions);
            
            _output.WriteLine($"Case-Insensitive - ListItemId: '{request?.ListItemId}', CorrelationId: '{request?.CorrelationId}'");
            
            Assert.NotNull(request);
            Assert.Equal("21", request.ListItemId);
            Assert.Equal("string", request.CorrelationId);
        }

        [Fact]
        public void TestExactMatchingJsonDeserialization()
        {
            // Test with exact property name matching
            string json = """
            {
              "ListItemId": "21",
              "CorrelationId": "string"
            }
            """;

            _output.WriteLine($"Testing JSON with exact case: {json}");

            var request = JsonSerializer.Deserialize<ProcessingRequest>(json);
            
            _output.WriteLine($"Exact Case - ListItemId: '{request?.ListItemId}', CorrelationId: '{request?.CorrelationId}'");
            
            Assert.NotNull(request);
            Assert.Equal("21", request.ListItemId);
            Assert.Equal("string", request.CorrelationId);
        }

        [Fact]
        public void TestMinimalJsonDeserialization()
        {
            // Test with just the required field
            string json = """
            {
              "listItemId": "21"
            }
            """;

            _output.WriteLine($"Testing minimal JSON: {json}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var request = JsonSerializer.Deserialize<ProcessingRequest>(json, jsonOptions);
            
            _output.WriteLine($"Minimal - ListItemId: '{request?.ListItemId}', CorrelationId: '{request?.CorrelationId}'");
            
            Assert.NotNull(request);
            Assert.Equal("21", request.ListItemId);
            Assert.True(string.IsNullOrEmpty(request.CorrelationId));
        }
    }
}