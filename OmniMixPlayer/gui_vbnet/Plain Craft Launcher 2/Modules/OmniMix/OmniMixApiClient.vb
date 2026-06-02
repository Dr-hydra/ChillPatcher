Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Class OmniMixBackendStatus
    Public Property IsOnline As Boolean
    Public Property Port As Integer?
    Public Property BaseUrl As String
    Public Property Message As String
    Public Property BackendPath As String
    Public Property StartedBackend As Boolean
End Class

Public Class OmniMixPlaylistData
    Public Property Tags As List(Of OmniMixTagInfo) = New List(Of OmniMixTagInfo)
    Public Property Albums As List(Of OmniMixAlbumInfo) = New List(Of OmniMixAlbumInfo)
    Public Property Songs As List(Of OmniMixSongInfo) = New List(Of OmniMixSongInfo)
End Class

Public Class OmniMixTagInfo
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property ModuleId As String = ""
    Public Property BitValue As Integer
    Public Property IsGrowable As Boolean
End Class

Public Class OmniMixAlbumInfo
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property TagId As String = ""
    Public Property ModuleId As String = ""
    Public Property CoverPath As String = ""
    Public Property SongCount As Integer
    Public Property IsGrowable As Boolean
End Class

Public Class OmniMixSongInfo
    Public Property Uuid As String = ""
    Public Property Title As String = ""
    Public Property Artist As String = ""
    Public Property AlbumId As String = ""
    Public Property Duration As Double
    Public Property ModuleId As String = ""
    Public Property CoverPath As String = ""
    Public Property CoverUrl As String = ""
    Public Property ImageUrl As String = ""
    Public Property IsFavorite As Boolean
    Public Property IsExcluded As Boolean
End Class

Public Class OmniMixTrackInfo
    Public Property Uuid As String = ""
    Public Property Title As String = ""
    Public Property Artist As String = ""
    Public Property AlbumId As String = ""
    Public Property Duration As Double
    Public Property ModuleId As String = ""
    Public Property CoverPath As String = ""
    Public Property CoverUrl As String = ""
    Public Property ImageUrl As String = ""
End Class

Public Class OmniMixQueueItemInfo
    Inherits OmniMixTrackInfo

    Public Property Index As Integer
End Class

Public Class OmniMixPlaylistSourceInfo
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property SongCount As Integer
End Class

Public Class OmniMixPlaybackInstanceInfo
    Public Property Id As String = ""
    Public Property ClientId As String = ""
    Public Property Role As String = ""
    Public Property Mode As String = ""
    Public Property Attached As Boolean
    Public Property IsPlaying As Boolean
    Public Property Position As Double
    Public Property Volume As Double = 1
    Public Property TargetLatency As Double = 0.1
    Public Property QueueCount As Integer
    Public Property QueueIndex As Integer = -1
    Public Property HistoryCount As Integer
    Public Property SampleRate As Integer
    Public Property Channels As Integer
    Public Property Shuffle As Boolean
    Public Property RepeatMode As String = "none"
    Public Property CurrentTrack As OmniMixTrackInfo
    Public Property ModId As String = ""
    Public Property GameName As String = ""

    Public ReadOnly Property IsServerManaged As Boolean
        Get
            Return String.Equals(Mode, "ServerManaged", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property
End Class

Public Class OmniMixInstanceStatsInfo
    Public Property InstanceCount As Integer
    Public Property AttachedAudioClients As Integer
    Public Property ControllerClients As Integer
    Public Property ObserverClients As Integer
    Public Property SharedMemoryBytes As Long
    Public Property ActiveDecoders As Integer
    Public Property TotalQueueItems As Integer
    Public Property HeartbeatTimeoutSeconds As Integer
    Public Property DetachedTtlSeconds As Integer
    Public Property CleanupIntervalSeconds As Integer
End Class

Public Class OmniMixArchiveInfo
    Public Property InstanceId As String = ""
    Public Property ModId As String = ""
    Public Property Mode As String = ""
    Public Property Label As String = ""
    Public Property ArchivedAt As String = ""

    Public ReadOnly Property DisplayName As String
        Get
            Return If(String.IsNullOrWhiteSpace(Label), InstanceId, Label)
        End Get
    End Property
End Class

Public Class OmniMixEqualizerPointInfo
    Public Property Id As String = ""
    Public Property Frequency As Double = 1000
    Public Property GainDb As Double = 0
    Public Property Q As Double = 1
    Public Property Type As String = "Peaking"
End Class

Public Class OmniMixEqualizerStateInfo
    Public Property Enabled As Boolean
    Public Property GlobalGainDb As Double
    Public Property SoftClipEnabled As Boolean = True
    Public Property Points As List(Of OmniMixEqualizerPointInfo) = New List(Of OmniMixEqualizerPointInfo)
End Class

Public Class OmniMixModuleInfo
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property Version As String = ""
    Public Property Priority As Integer
    Public Property LoadedAt As String = ""
    Public Property Enabled As Boolean
    <JsonPropertyName("hasSettingsUI")>
    Public Property HasSettingsUI As Boolean
    Public Property HasQuickLinks As Boolean
    Public Property LinkEntries As List(Of OmniMixModuleLinkEntryInfo) = New List(Of OmniMixModuleLinkEntryInfo)
End Class

Public Class OmniMixModuleLinkEntryInfo
    Public Property Id As String = ""
    Public Property Title As String = ""
    Public Property Icon As String = ""
    Public Property Svg As String = ""
    Public Property BackgroundColor As String = ""
    Public Property IconColor As String = ""
End Class

Public Class OmniMixRawNodeData
    Public Property Id As String = ""
    <JsonPropertyName("node-type")>
    Public Property NodeType As String = ""
    Public Property Text As String = ""
    <JsonPropertyName("font-size")>
    Public Property FontSize As Double = 14
    Public Property Color As String = ""
    Public Property Direction As String = ""
    <JsonPropertyName("cross-axis-align")>
    Public Property CrossAxisAlignment As String = ""
    Public Property Spacing As Double = 8
    Public Property Padding As Double
    Public Property Children As List(Of OmniMixRawNodeData) = New List(Of OmniMixRawNodeData)
    Public Property Value As String = ""
    <JsonPropertyName("input-type")>
    Public Property InputType As String = "text"
    <JsonPropertyName("button-variant")>
    Public Property ButtonVariant As String = "primary"
    Public Property Checked As Boolean
    Public Property Source As String = ""
    <JsonPropertyName("image-width")>
    Public Property ImageWidth As Double = 200
    <JsonPropertyName("image-height")>
    Public Property ImageHeight As Double = 200
    <JsonPropertyName("image-fit")>
    Public Property ImageFit As String = "contain"
    <JsonPropertyName("selected-value")>
    Public Property SelectedValue As String = ""
    Public Property Options As List(Of OmniMixRawOptionData) = New List(Of OmniMixRawOptionData)
    Public Property Items As List(Of OmniMixRawNodeData) = New List(Of OmniMixRawNodeData)
End Class

Public Class OmniMixRawOptionData
    Public Property Value As String = ""
    Public Property Label As String = ""
End Class

Public Module OmniMixApiClient

    Private ReadOnly Client As New HttpClient With {
        .Timeout = TimeSpan.FromSeconds(3)
    }
    Private ReadOnly JsonOptions As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True
    }

    Public Async Function DiscoverAsync() As Task(Of OmniMixBackendStatus)
        Dim Candidates As New List(Of Integer)
        Dim FilePort = ReadPortFile()
        If FilePort.HasValue Then Candidates.Add(FilePort.Value)
        If Not Candidates.Contains(17890) Then Candidates.Add(17890)

        For Each CandidatePort In Candidates
            If Await CheckHealthAsync(CandidatePort) Then
                Return New OmniMixBackendStatus With {
                    .IsOnline = True,
                    .Port = CandidatePort,
                    .BaseUrl = $"http://127.0.0.1:{CandidatePort}",
                    .Message = $"已连接 OmniMix 后端（127.0.0.1:{CandidatePort}）"
                }
            End If
        Next

        Return New OmniMixBackendStatus With {
            .IsOnline = False,
            .Port = FilePort,
            .BaseUrl = If(FilePort.HasValue, $"http://127.0.0.1:{FilePort.Value}", ""),
            .Message = If(FilePort.HasValue,
                $"发现端口文件（{FilePort.Value}），但 /api/health 暂不可用。",
                "未发现可用的 OmniMix 后端。")
        }
    End Function

    Public Async Function GetPlaylistAsync(BaseUrl As String) As Task(Of OmniMixPlaylistData)
        Dim Playlist = Await GetJsonAsync(Of OmniMixPlaylistData)(BaseUrl, "/api/playlist")
        Return If(Playlist, New OmniMixPlaylistData)
    End Function

    Public Async Function GetInstancesAsync(BaseUrl As String) As Task(Of List(Of OmniMixPlaybackInstanceInfo))
        Dim Instances = Await GetJsonAsync(Of List(Of OmniMixPlaybackInstanceInfo))(BaseUrl, "/api/instances")
        Return If(Instances, New List(Of OmniMixPlaybackInstanceInfo))
    End Function

    Public Async Function GetInstanceStatsAsync(BaseUrl As String) As Task(Of OmniMixInstanceStatsInfo)
        Dim Stats = Await GetJsonAsync(Of OmniMixInstanceStatsInfo)(BaseUrl, "/api/instances/stats")
        Return If(Stats, New OmniMixInstanceStatsInfo)
    End Function

    Public Async Function GetInstanceQueueAsync(BaseUrl As String, InstanceId As String) As Task(Of List(Of OmniMixQueueItemInfo))
        Dim Queue = Await GetJsonAsync(Of List(Of OmniMixQueueItemInfo))(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue")
        Return If(Queue, New List(Of OmniMixQueueItemInfo))
    End Function

    Public Async Function GetPlaylistSourcesAsync(BaseUrl As String, InstanceId As String) As Task(Of List(Of OmniMixPlaylistSourceInfo))
        Dim Sources = Await GetJsonAsync(Of List(Of OmniMixPlaylistSourceInfo))(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/playlist/sources")
        Return If(Sources, New List(Of OmniMixPlaylistSourceInfo))
    End Function

    Public Async Function AddPlaylistSourceAsync(BaseUrl As String, InstanceId As String, SourceId As String, SourceName As String, Uuids As IEnumerable(Of String)) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/playlist/sources", New With {
            .index = -1,
            .source = New With {
                .id = If(SourceId, ""),
                .name = If(SourceName, ""),
                .uuids = If(Uuids, Enumerable.Empty(Of String)()).Where(Function(Uuid) Not String.IsNullOrWhiteSpace(Uuid)).ToArray()
            }
        })
    End Function

    Public Async Function RemovePlaylistSourceAsync(BaseUrl As String, InstanceId As String, SourceId As String) As Task
        Await DeleteAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/playlist/sources/" & Uri.EscapeDataString(SourceId))
    End Function

    Public Async Function GetInstanceHistoryAsync(BaseUrl As String, InstanceId As String) As Task(Of List(Of OmniMixQueueItemInfo))
        Dim History = Await GetJsonAsync(Of List(Of OmniMixQueueItemInfo))(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/history")
        Return If(History, New List(Of OmniMixQueueItemInfo))
    End Function

    Public Async Function ConnectControllerAsync(BaseUrl As String, Optional ClientId As String = "vbnet-gui") As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/connect", New With {
            .clientId = ClientId,
            .role = "controller",
            .mode = "server"
        })
    End Function

    Public Async Function PlayAsync(BaseUrl As String, InstanceId As String, Optional Uuid As String = "") As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/play", New With {
            .uuid = If(Uuid, "")
        })
    End Function

    Public Async Function AddToQueueAsync(BaseUrl As String, InstanceId As String, Uuid As String) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue", New With {
            .uuid = If(Uuid, "")
        })
    End Function

    Public Async Function AddToQueueRangeAsync(BaseUrl As String, InstanceId As String, Uuids As IEnumerable(Of String)) As Task
        Dim SongUuids = If(Uuids, Enumerable.Empty(Of String)()).
            Where(Function(Uuid) Not String.IsNullOrWhiteSpace(Uuid)).
            Distinct().
            ToArray()
        If SongUuids.Length = 0 Then Return

        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue/insert", New With {
            .uuids = SongUuids,
            .index = Integer.MaxValue
        })
    End Function

    Public Async Function RemoveQueueItemAsync(BaseUrl As String, InstanceId As String, Index As Integer) As Task
        Await DeleteAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue/" & Index)
    End Function

    Public Async Function MoveQueueItemAsync(BaseUrl As String, InstanceId As String, FromIndex As Integer, ToIndex As Integer) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue/move", New With {
            .from = FromIndex,
            .to = ToIndex
        })
    End Function

    Public Async Function ClearQueueAsync(BaseUrl As String, InstanceId As String) As Task
        Await PostEmptyAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/queue/clear")
    End Function

    Public Async Function RemoveHistoryItemAsync(BaseUrl As String, InstanceId As String, Index As Integer) As Task
        Await DeleteAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/history/" & Index)
    End Function

    Public Async Function MoveHistoryItemAsync(BaseUrl As String, InstanceId As String, FromIndex As Integer, ToIndex As Integer) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/history/move", New With {
            .from = FromIndex,
            .to = ToIndex
        })
    End Function

    Public Async Function ClearHistoryAsync(BaseUrl As String, InstanceId As String) As Task
        Await PostEmptyAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/history/clear")
    End Function

    Public Async Function SendInstanceCommandAsync(BaseUrl As String, InstanceId As String, Command As String) As Task
        Await PostEmptyAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/" & Uri.EscapeDataString(Command))
    End Function

    Public Async Function SeekInstanceAsync(BaseUrl As String, InstanceId As String, Position As Double) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/seek", New With {
            .position = Position
        })
    End Function

    Public Async Function SetInstanceVolumeAsync(BaseUrl As String, InstanceId As String, Volume As Double) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/volume", New With {
            .volume = Volume
        })
    End Function

    Public Async Function SetInstanceLatencyAsync(BaseUrl As String, InstanceId As String, Latency As Double) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/latency", New With {
            .latency = Latency
        })
    End Function

    Public Async Function SetInstanceShuffleAsync(BaseUrl As String, InstanceId As String, Enabled As Boolean) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/shuffle", New With {
            .enabled = Enabled
        })
    End Function

    Public Async Function SetInstanceRepeatModeAsync(BaseUrl As String, InstanceId As String, Mode As String) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/repeat", New With {
            .mode = If(Mode, "none")
        })
    End Function

    Public Async Function GetInstanceEqualizerAsync(BaseUrl As String, InstanceId As String) As Task(Of OmniMixEqualizerStateInfo)
        Dim State = Await GetJsonAsync(Of OmniMixEqualizerStateInfo)(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/equalizer")
        Return If(State, New OmniMixEqualizerStateInfo)
    End Function

    Public Async Function PutInstanceEqualizerAsync(BaseUrl As String, InstanceId As String, State As OmniMixEqualizerStateInfo) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/equalizer", If(State, New OmniMixEqualizerStateInfo))
    End Function

    Public Async Function GetInstanceEqualizerPresetsAsync(BaseUrl As String, InstanceId As String) As Task(Of Dictionary(Of String, OmniMixEqualizerStateInfo))
        Dim Presets = Await GetJsonAsync(Of Dictionary(Of String, OmniMixEqualizerStateInfo))(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/equalizer/presets")
        Return If(Presets, New Dictionary(Of String, OmniMixEqualizerStateInfo))
    End Function

    Public Async Function GetConfigAsync(BaseUrl As String) As Task(Of Dictionary(Of String, JsonElement))
        Dim Config = Await GetJsonAsync(Of Dictionary(Of String, JsonElement))(BaseUrl, "/api/config")
        Return If(Config, New Dictionary(Of String, JsonElement))
    End Function

    Public Async Function PutConfigRawAsync(BaseUrl As String, Updates As Dictionary(Of String, Object)) As Task
        Await PutJsonAsync(BaseUrl, "/api/config", Updates)
    End Function

    Public Async Function SaveConfigAsync(BaseUrl As String) As Task
        Await PostEmptyAsync(BaseUrl, "/api/config/save")
    End Function

    Public Async Function StopBackendAsync(BaseUrl As String) As Task
        Await PostEmptyAsync(BaseUrl, "/api/backend/stop")
    End Function

    Public Async Function SetActiveInstanceAsync(BaseUrl As String, InstanceId As String) As Task
        Await PutConfigRawAsync(BaseUrl, New Dictionary(Of String, Object) From {
            {"active_instance", If(InstanceId, "")}
        })
        Await SaveConfigAsync(BaseUrl)
    End Function

    Public Async Function GetArchivesAsync(BaseUrl As String) As Task(Of List(Of OmniMixArchiveInfo))
        Dim Archives = Await GetJsonAsync(Of List(Of OmniMixArchiveInfo))(BaseUrl, "/api/instances/archives")
        Return If(Archives, New List(Of OmniMixArchiveInfo))
    End Function

    Public Async Function DeleteInstanceAsync(BaseUrl As String, InstanceId As String) As Task
        Await DeleteAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId))
    End Function

    Public Async Function SetInstanceMetaAsync(BaseUrl As String, InstanceId As String, ModId As String, GameName As String, Mode As String) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/meta", New With {
            .modId = If(ModId, ""),
            .gameName = If(GameName, ""),
            .mode = If(Mode, "")
        })
    End Function

    Public Async Function UpdateInstanceProfileAsync(BaseUrl As String, InstanceId As String, Profile As Dictionary(Of String, Object)) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/profile", If(Profile, New Dictionary(Of String, Object)))
    End Function

    Public Async Function ArchiveInstanceAsync(BaseUrl As String, InstanceId As String, Label As String) As Task
        Await PostJsonAsync(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/archive", New With {
            .label = If(Label, "")
        })
    End Function

    Public Async Function RenameArchiveAsync(BaseUrl As String, InstanceId As String, Label As String) As Task
        Await PutJsonAsync(BaseUrl, "/api/instances/archives/" & Uri.EscapeDataString(InstanceId) & "/rename", New With {
            .label = If(Label, "")
        })
    End Function

    Public Async Function DeleteArchiveAsync(BaseUrl As String, InstanceId As String) As Task
        Await DeleteAsync(BaseUrl, "/api/instances/archives/" & Uri.EscapeDataString(InstanceId))
    End Function

    Public Async Function InheritFromArchiveAsync(BaseUrl As String, InstanceId As String, ArchiveId As String) As Task(Of Dictionary(Of String, JsonElement))
        Dim Result = Await PostEmptyForResultAsync(Of Dictionary(Of String, JsonElement))(BaseUrl, "/api/instances/" & Uri.EscapeDataString(InstanceId) & "/inherit/" & Uri.EscapeDataString(ArchiveId))
        Return If(Result, New Dictionary(Of String, JsonElement))
    End Function

    Public Async Function GetModulesAsync(BaseUrl As String) As Task(Of List(Of OmniMixModuleInfo))
        Dim Modules = Await GetJsonAsync(Of List(Of OmniMixModuleInfo))(BaseUrl, "/api/modules")
        Return If(Modules, New List(Of OmniMixModuleInfo))
    End Function

    Public Async Function SetModuleEnabledAsync(BaseUrl As String, ModuleId As String, Enabled As Boolean) As Task
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & "/api/modules/" & Uri.EscapeDataString(ModuleId)
        Dim Body = JsonSerializer.Serialize(New With {.enabled = Enabled}, JsonOptions)
        Using Content As New StringContent(Body, Encoding.UTF8, "application/json")
            Using Response = Await Client.PostAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
            End Using
        End Using
    End Function

    Public Async Function GetModuleUiAsync(BaseUrl As String, ModuleId As String) As Task(Of OmniMixRawNodeData)
        Return Await GetJsonAsync(Of OmniMixRawNodeData)(BaseUrl, "/api/modules/" & Uri.EscapeDataString(ModuleId) & "/ui")
    End Function

    Public Async Function GetModuleSettingsUiAsync(BaseUrl As String, ModuleId As String) As Task(Of OmniMixRawNodeData)
        Return Await GetJsonAsync(Of OmniMixRawNodeData)(BaseUrl, "/api/modules/" & Uri.EscapeDataString(ModuleId) & "/settings")
    End Function

    Public Async Function GetModuleLinkUiAsync(BaseUrl As String, ModuleId As String, LinkId As String) As Task(Of OmniMixRawNodeData)
        Return Await GetJsonAsync(Of OmniMixRawNodeData)(BaseUrl, "/api/modules/" & Uri.EscapeDataString(ModuleId) & "/link/" & Uri.EscapeDataString(LinkId))
    End Function

    Private Async Function PostJsonAsync(BaseUrl As String, ApiPath As String, BodyObject As Object) As Task
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Dim Body = JsonSerializer.Serialize(BodyObject, JsonOptions)
        Using Content As New StringContent(Body, Encoding.UTF8, "application/json")
            Using Response = Await Client.PostAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
            End Using
        End Using
    End Function

    Private Async Function PostJsonForResultAsync(Of T)(BaseUrl As String, ApiPath As String, BodyObject As Object) As Task(Of T)
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Dim Body = JsonSerializer.Serialize(BodyObject, JsonOptions)
        Using Content As New StringContent(Body, Encoding.UTF8, "application/json")
            Using Response = Await Client.PostAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
                Dim Json = Await Response.Content.ReadAsStringAsync()
                Return JsonSerializer.Deserialize(Of T)(Json, JsonOptions)
            End Using
        End Using
    End Function

    Private Async Function PostEmptyAsync(BaseUrl As String, ApiPath As String) As Task
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Using Content As New StringContent("", Encoding.UTF8, "application/json")
            Using Response = Await Client.PostAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
            End Using
        End Using
    End Function

    Private Async Function PostEmptyForResultAsync(Of T)(BaseUrl As String, ApiPath As String) As Task(Of T)
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Using Content As New StringContent("", Encoding.UTF8, "application/json")
            Using Response = Await Client.PostAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
                Dim Json = Await Response.Content.ReadAsStringAsync()
                Return JsonSerializer.Deserialize(Of T)(Json, JsonOptions)
            End Using
        End Using
    End Function

    Private Async Function DeleteAsync(BaseUrl As String, ApiPath As String) As Task
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Using Response = Await Client.DeleteAsync(RequestUrl)
            Response.EnsureSuccessStatusCode()
        End Using
    End Function

    Private Async Function PutJsonAsync(BaseUrl As String, ApiPath As String, BodyObject As Object) As Task
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Dim Body = JsonSerializer.Serialize(BodyObject, JsonOptions)
        Using Content As New StringContent(Body, Encoding.UTF8, "application/json")
            Using Response = Await Client.PutAsync(RequestUrl, Content)
                Response.EnsureSuccessStatusCode()
            End Using
        End Using
    End Function

    Private Async Function GetJsonAsync(Of T)(BaseUrl As String, ApiPath As String) As Task(Of T)
        Dim RequestUrl = BaseUrl.TrimEnd("/"c) & ApiPath
        Using Response = Await Client.GetAsync(RequestUrl)
            Response.EnsureSuccessStatusCode()
            Dim Json = Await Response.Content.ReadAsStringAsync()
            Return JsonSerializer.Deserialize(Of T)(Json, JsonOptions)
        End Using
    End Function

    Private Async Function CheckHealthAsync(CandidatePort As Integer) As Task(Of Boolean)
        Try
            Using Response = Await Client.GetAsync($"http://127.0.0.1:{CandidatePort}/api/health")
                Return Response.IsSuccessStatusCode
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Function ReadPortFile() As Integer?
        For Each DirectoryPath In GetPortFileDirs()
            If String.IsNullOrWhiteSpace(DirectoryPath) Then Continue For
            Try
                Dim FilePath = Path.Combine(DirectoryPath, "omnimix_port.txt")
                If Not FileUtils.Exists(FilePath) Then Continue For
                Dim Raw = File.ReadAllText(FilePath).Trim()
                Dim ParsedPort As Integer
                If Integer.TryParse(Raw, ParsedPort) AndAlso ParsedPort > 0 AndAlso ParsedPort < 65536 Then Return ParsedPort
            Catch
            End Try
        Next
        Return Nothing
    End Function

    Private Function GetPortFileDirs() As IEnumerable(Of String)
        Dim Dirs As New List(Of String) From {
            AppContext.BaseDirectory,
            Path.GetTempPath()
        }

        Dim PublicDir = Environment.GetEnvironmentVariable("PUBLIC")
        If Not String.IsNullOrWhiteSpace(PublicDir) Then Dirs.Insert(1, Path.Combine(PublicDir, "OmniMixPlayer"))

        Return Dirs.Distinct(StringComparer.OrdinalIgnoreCase)
    End Function

End Module
