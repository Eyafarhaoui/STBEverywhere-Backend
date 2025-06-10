using STBEverywhere_Back_SharedModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestSprint2
{
    namespace Tests.Mocks.ClientInfo
    {
        public class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Client _client;

            public FakeHttpMessageHandler(Client client)
            {
                _client = client;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var json = JsonSerializer.Serialize(_client);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
        }
    }

}
