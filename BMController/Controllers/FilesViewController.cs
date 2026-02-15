using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BMController.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesViewController : ControllerBase
    {
        // GET: api/<FilesViewController>
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        // GET api/<FilesViewController>/5
        [HttpGet("{ip}")]
        public async Task<IActionResult> Get(string ip)
        {
            // Basic parse (accepts IPv4 and IPv6)
            if (!IPAddress.TryParse(ip, out var address))
            {
                return BadRequest("Invalid IP address format.");
            }

            var client = new FtpClient(ip);
            client.AutoConnect();

            StringBuilder filesFound = new StringBuilder();
            PrintFiles(client, filesFound);

            return Ok(filesFound.ToString());
        }

        static void PrintFiles(FtpClient client, StringBuilder filesFound, string path = "")
        {
            var items = client.GetListing(path);
            foreach (var item in items)
            {
                string itemData = $"{item.FullName} - {FormatBytes(item.Size)} - {item.Modified}";
                Console.WriteLine(itemData);
                filesFound.AppendLine(itemData);
                if (item.Type == FtpObjectType.Directory)
                {
                    PrintFiles(client, filesFound, item.FullName);
                }
            }
        }

        // Convert a byte count to a human-readable string.
        // Defaults to binary units (KiB, MiB, ...) using 1024. Set si = true for decimal (kB, MB, ...).
        static string FormatBytes(long bytes, bool si = false)
        {
            if (bytes == 0) return "0 B";

            long absBytes = Math.Abs(bytes);
            int unit = si ? 1000 : 1024;
            string[] suffixes = si
                ? new[] { "B", "kB", "MB", "GB", "TB", "PB", "EB" }
                : new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };

            int idx = 0;
            double value = absBytes;
            while (value >= unit && idx < suffixes.Length - 1)
            {
                value /= unit;
                idx++;
            }

            // Preserve sign for negative values.
            string sign = bytes < 0 ? "-" : "";

            // Format keeping one decimal for values >= 10 with fraction, otherwise show up to two decimals.
            string formatted = value >= 10 ? value.ToString("0.#") : value.ToString("0.##");
            return $"{sign}{formatted} {suffixes[idx]}";
        }

        //// POST api/<FilesViewController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<FilesViewController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<FilesViewController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
