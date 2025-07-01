using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
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
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string format, [FromForm] string gptToken, [FromForm] string gptModel, [FromForm] string description)
        {
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

            var prompt = $"Faça como um profissional refine e faça a seguinte solicitação referente ao arquivo {Path.GetFileName(file.FileName)}: \n {description}";
            if (format == "text")
            {
                prompt += "\nO resumo deve estar no formato de texto com os titulos em destaque negrito.";
            }
            else if (format == "json")
            {
                prompt += "\nO resumo deve estar no formato de JSON, incluir o nome do arquivo como na chave arquivo, tendo os titulos como chaves, o formato gerado deve ser livre de caracteres que nao pertencem ao JSON";
            }
            else
            {
                prompt += "O resumo deve estar no formato de hmtl simples,Considere apenas a estrutura interna do <body>, ou seja, não inclua a tag <body> em si, Use somente as seguintes tags para a estrutura: <h1>, <h2>, <h3>, <h4>, <ul>, <li>, <strong>, <p>, e quebras de linha se necessário. Segue um modelo: \n Titulo na Tag<h1> \n Topicos no strong <h2><strong>...</strong></h2> e subitens em listas não ordenadas <ul><li>...</li></ul>";
            }
            prompt += "\nSem comentarios apenas a analise feita";
            prompt += "\nRetire valores vazios ou nulos";
            if(format != "json") prompt += "\nAjuste gramaticalmente os titulos";
            prompt += $"\nSegue os dados no formato de JSON gerado a partir de um CSV: \n {listConvertida}";
            var result = await GenerateAIResult(prompt, gptToken, gptModel);
            result = result.Replace("```", "").Replace("´´´", "").Replace("html", "").Replace("\r\n", " ").Replace("\r", " ");

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
                              $"PrincipalFuncao = '{record.PrincipalFuncao.Replace("\r\n"," ")}', " +
                              $"NivelExperiencia = '{record.NivelExperiencia.Replace("\r\n", " ")}', " +
                              $"ExperienciaAtual = '{record.ExperienciaAtual.Replace("\r\n", " ")}', " +
                              $"ExperienciaAtual2 = '{record.ExperienciaAtual2.Replace("\r\n", " ")}', " +
                              $"MotivacaoInscricao = '{record.MotivacaoInscricao.Replace("\r\n", " ")}', " +
                              $"OQuePerguntaria = '{record.OQuePerguntaria.Replace("\r\n", " ")}', " +
                              $"TopicosInteresse = '{record.TopicosInteresse.Replace("\r\n", " ")}', ");
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

            var json = System.Text.Json.JsonSerializer.Serialize(payload);

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
