using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DanMu.Controllers
{
    [Route("api/[controller]")]
    public class DanMuController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<DanmuData> Get()
        {
            return SocketHandler.historicalMessg;
        }

        // GET api/values/5
        [HttpGet("online")]
        public int GetOnline()
        {
            return SocketHandler.Sockets.Count;
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
