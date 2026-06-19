using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuntime;

namespace ETOS.Backend.Tests.Fixtures;

public sealed class MockAgentRuntimeHttpHandler : HttpMessageHandler
{
    public const string ValidChatAnswerOutput =
        """{"answer":"Governed agent response.","evidence":[{"contextId":"ctx-1","contextType":"part","safeSummary":"Linked part evidence."}],"confidence":{"overall":0.9,"notes":"Mock runtime"}}""";

    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public HttpRequestMessage? LastRequest { get; private set; }

    public static MockAgentRuntimeHttpHandler CreateSuccessHandler(string? structuredOutputJson = null)
    {
        var handler = new MockAgentRuntimeHttpHandler();
        handler.EnqueueSuccess(structuredOutputJson);
        return handler;
    }

    public void EnqueueSuccess(string? structuredOutputJson = null)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                status = AgentRuntimeExecutionStatuses.Succeeded,
                structuredOutputJson = structuredOutputJson ?? ValidChatAnswerOutput,
                traceNotes = new[] { "mock-agent-runtime" },
                modelUsed = "mock-v1",
                fallbackApplied = false
            })
        });
    }

    public void EnqueueResponse(HttpStatusCode statusCode, object body)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(body)
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"Unexpected agent runtime HTTP request: {request.Method} {request.RequestUri}");
        }

        LastRequest = request;
        return Task.FromResult(_responses.Dequeue()(request));
    }
}
