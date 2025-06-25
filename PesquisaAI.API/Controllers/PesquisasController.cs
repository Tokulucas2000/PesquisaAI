using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
namespace PesquisaAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PesquisasController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string format)
        {
            //format pode receber valores como text, json ou html
            var prompt = $"Baseado no json gere um relatório no formato de {format} com o titulo sendo Relatório do arquivo {Path.GetFileNameWithoutExtension(file.FileName)}, tendo como exemplo o tópico Idade que faz referencia ao campo idade, contendo uma bullet list com a idade e a quantidade de vezes que se repete, aplicar esse modelo para todos os campos que possuem mais de valores em comum, os demais devem apenas gerar o topico com uma list dos resultados. Sem valores vazios. {(format != "json" ? "todos os tópicos em negrito" : "")}";
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
            var result = GenerateAIResult(listRecords, prompt);
            return Ok(result);
        }

        private object GenerateAIResult(List<PesquisaCsvRecord> records, string prompt)
        {
            //requisicao da openAI

            return new { records };
        }
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
