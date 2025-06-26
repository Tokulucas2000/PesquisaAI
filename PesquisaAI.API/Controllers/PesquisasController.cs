using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace PesquisaAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PesquisasController : ControllerBase
    {
        readonly HttpClient _httpClient;

        public PesquisasController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string format, [FromForm] string gptToken, [FromForm] string gptModel)
        {
            //format pode receber valores como text, json ou html
            var prompt = $"Baseado no json gere um relatório no formato de {(format == "html" ? "html com apenas o conteúdo dentro do <body>, sem a tag <body> e <html>" : format)} com o titulo sendo Relatório do arquivo {Path.GetFileNameWithoutExtension(file.FileName)}, {(format == "html" ? "gere apenas o conteúdo que estaria dentro da tag <body>, sem incluir a tag <body> ou <html>. Use apenas <h1>, <h2>, <ul>, <li> etc. Siga esse modelo: Título no <h1>Tópicos em <h2><strong>...</strong></h2>Lista com contagem: <li>Valor (X)</li>Ignore campos com valores vazios" : "tendo como exemplo o tópico Idade que faz referencia ao campo idade, contendo uma bullet list com a idade e a quantidade de vezes que se repete, aplicar esse modelo para todos os campos que possuem mais de valores em comum, os demais devem apenas gerar o topico com uma list dos resultados. Sem valores vazios." )} {(format != "json" ? "todos os tópicos em negrito " : " ")}";
            var listRecords = new List<PesquisaCsvRecord>();
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                    BadDataFound = null,
                    MissingFieldFound = null
                }))
                {
                    var records = csv.GetRecordsAsync<PesquisaCsvRecord>();

                    await foreach (var record in records)
                    {
                        listRecords.Add(record);
                    }
                }
            }
            var listConvertida = await ConvertListToStructuredString(listRecords);
            prompt += listConvertida.Replace("\r\n", " "); 
            var result = await GenerateAIResult(prompt, gptToken, gptModel);
            result = result.Replace("```", "").Replace("´´´", "");
            return Ok(result);
        }

        private async Task<string> ConvertListToStructuredString(List<PesquisaCsvRecord> records)
        {
            if (records == null || records.Count == 0)
                return "Lista vazia";

            // Executa a construção da string em uma thread separada
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();

                foreach (var record in records)
                {
                    sb.Append($"Idade = '{record.Idade}', " +
                              $"PrincipalFuncao = '{record.PrincipalFuncao}', " +
                              $"NivelExperiencia = '{record.NivelExperiencia}', " +
                              $"ExperienciaAtual = '{record.ExperienciaAtual}', " +
                              $"ExperienciaAtual2 = '{record.ExperienciaAtual2}', " +
                              $"MotivacaoInscricao = '{record.MotivacaoInscricao}', " +
                              $"OQuePerguntaria = '{record.OQuePerguntaria}', " +
                              $"TopicosInteresse = '{record.TopicosInteresse}', ");
                }

                // Remove a vírgula e espaço extra do último registro
                if (records.Count > 0)
                    sb.Length -= 2;

                return sb.ToString();
            });
        }
        private async Task<string> GenerateAIResult(string prompt, string gptToken, string gptModel)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gptToken);

            var payload = new
            {
                model = "gpt-4.1-nano",
                input = prompt
            };

            var json = JsonSerializer.Serialize(payload);

            requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            var responseJson = await response.Content.ReadAsStringAsync();

            var openAiResponse = await response.Content.ReadFromJsonAsync<OpenAIResponse>();

            return openAiResponse?.Output?.First()?.Content?.First()?.Text ?? string.Empty;
        }
    }
    public class OpenAIResponse
    {
        [JsonPropertyName("output")]
        public List<Output> Output { get; set; }
    }

    public class Output
    {
        [JsonPropertyName("content")]
        public List<Content> Content { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("annotations")]
        public List<object> Annotations { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
    public class PesquisaCsvRecord
    {
        public string Idade { get; set; }
        public string PrincipalFuncao { get; set; }
        public string NivelExperiencia { get; set; }
        public string ExperienciaAtual { get; set; }
        public string ExperienciaAtual2 { get; set; }
        public string MotivacaoInscricao { get; set; }
        public string OQuePerguntaria { get; set; }
        public string TopicosInteresse { get; set; }
    }
}
