Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Public Class OmniMixWsClient
    Implements IDisposable

    Private Socket As ClientWebSocket
    Private ReceiveTask As Task
    Private ReadOnly SendLock As New SemaphoreSlim(1, 1)
    Private ReadOnly JsonOptions As New JsonSerializerOptions With {
        .PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }
    Private IsDisposed As Boolean = False

    Public ReadOnly Property IsConnected As Boolean
        Get
            Return Socket IsNot Nothing AndAlso Socket.State = WebSocketState.Open
        End Get
    End Property

    Public Async Function ConnectAsync(BaseUrl As String) As Task
        If IsConnected Then Return
        DisposeSocket()

        Dim Uri = BuildWsUri(BaseUrl)
        Socket = New ClientWebSocket()
        Await Socket.ConnectAsync(Uri, CancellationToken.None)
        ReceiveTask = Task.Run(AddressOf ReceiveLoopAsync)
    End Function

    Public Async Function SendUiEventAsync(BaseUrl As String, ModuleId As String, NodeId As String, Action As String, Value As String, Optional UiKind As String = "default", Optional LinkId As String = "") As Task
        If String.IsNullOrWhiteSpace(ModuleId) Then Return
        Await ConnectAsync(BaseUrl)

        Dim Message = JsonSerializer.Serialize(New With {
            .type = "ui_event",
            .moduleId = ModuleId,
            .uiKind = If(String.IsNullOrWhiteSpace(UiKind), "default", UiKind),
            .linkId = If(LinkId, ""),
            .event = New With {
                .nodeId = If(NodeId, ""),
                .action = If(Action, ""),
                .value = If(Value, "")
            }
        }, JsonOptions)

        Dim Bytes = Encoding.UTF8.GetBytes(Message)
        Await SendLock.WaitAsync()
        Try
            If IsConnected Then
                Await Socket.SendAsync(New ArraySegment(Of Byte)(Bytes), WebSocketMessageType.Text, True, CancellationToken.None)
            End If
        Finally
            SendLock.Release()
        End Try
    End Function

    Private Async Function ReceiveLoopAsync() As Task
        Dim Buffer(4095) As Byte
        Try
            While IsConnected
                Dim Result = Await Socket.ReceiveAsync(New ArraySegment(Of Byte)(Buffer), CancellationToken.None)
                If Result.MessageType = WebSocketMessageType.Close Then Exit While
            End While
        Catch
        End Try
    End Function

    Private Shared Function BuildWsUri(BaseUrl As String) As Uri
        Dim Builder As New UriBuilder(BaseUrl)
        Builder.Scheme = If(Builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase), "wss", "ws")
        Builder.Path = "/ws"
        Builder.Query = ""
        Return Builder.Uri
    End Function

    Private Sub DisposeSocket()
        If Socket Is Nothing Then Return
        Try
            If Socket.State = WebSocketState.Open OrElse Socket.State = WebSocketState.CloseReceived Then
                Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "OmniMix GUI closing", CancellationToken.None).Wait(300)
            End If
        Catch
        End Try
        Socket.Dispose()
        Socket = Nothing
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If IsDisposed Then Return
        IsDisposed = True
        DisposeSocket()
        SendLock.Dispose()
    End Sub
End Class
