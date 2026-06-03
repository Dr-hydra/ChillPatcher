Imports System.Diagnostics
Imports System.IO

Public Module OmniMixBackendManager

    Private Const BackendExeName As String = "OmniMixPlayer.Backend.exe"
    Public Async Function EnsureStartedAsync() As Task(Of OmniMixBackendStatus)
        Dim BackendPath = FindBackendExe()
        Dim Status = Await OmniMixApiClient.DiscoverAsync()
        If Status.IsOnline Then
            Status.Message = "已发现正在运行的 OmniMix 后端。"
            Return Status
        End If

StartBundledBackend:
        If String.IsNullOrWhiteSpace(BackendPath) Then
            Return New OmniMixBackendStatus With {
                .IsOnline = False,
                .Message = "未找到 OmniMixPlayer.Backend.exe，无法自动启动后端。"
            }
        End If

        Try
            Dim GuiDir = PathExeFolder.TrimEnd("\"c, "/"c)
            StartProcess(New ProcessStartInfo With {
                .FileName = BackendPath,
                .Arguments = $"--port-file-dir=""{GuiDir}""",
                .WorkingDirectory = Path.GetDirectoryName(BackendPath),
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .WindowStyle = ProcessWindowStyle.Hidden
            })
        Catch ex As Exception
            Return New OmniMixBackendStatus With {
                .IsOnline = False,
                .BackendPath = BackendPath,
                .Message = "启动 OmniMix 后端失败：" & ex.Message
            }
        End Try

        For i = 0 To 39
            Await Task.Delay(500)

            Status = Await OmniMixApiClient.DiscoverAsync()
            If Status.IsOnline Then
                Status.BackendPath = BackendPath
                Status.StartedBackend = True
                Status.Message = "已启动并连接 OmniMix 后端。"
                Return Status
            End If

            If i > 2 AndAlso i Mod 4 = 3 AndAlso Not IsBackendProcessRunning() Then Exit For
        Next

        Return New OmniMixBackendStatus With {
            .IsOnline = False,
            .BackendPath = BackendPath,
            .Message = "已尝试启动 OmniMix 后端，但 /api/health 未在等待时间内就绪。"
        }
    End Function

    Public Function FindBackendExe() As String
        Dim ConfiguredPath = GetConfiguredBackendPath()
        If Not String.IsNullOrWhiteSpace(ConfiguredPath) Then
            Try
                Dim FullConfiguredPath = Path.GetFullPath(ConfiguredPath)
                If File.Exists(FullConfiguredPath) Then Return FullConfiguredPath
            Catch
            End Try
        End If

        Return FindDefaultBackendExe()
    End Function

    Public Function FindDefaultBackendExe() As String
        For Each Candidate In GetBackendExeCandidates()
            Try
                Dim FullPath = Path.GetFullPath(Candidate)
                If File.Exists(FullPath) Then Return FullPath
            Catch
            End Try
        Next
        Return Nothing
    End Function

    Public Function GetConfiguredBackendPath() As String
        Try
            Return Settings.Get(Of String)("OmniMixBackendPath")
        Catch
            Return ""
        End Try
    End Function

    Public Sub SetConfiguredBackendPath(BackendPath As String)
        Settings.Set("OmniMixBackendPath", If(BackendPath, "").Trim())
    End Sub

    Private Function GetBackendExeCandidates() As IEnumerable(Of String)
        Dim BaseDir = PathExeFolder
        Return New List(Of String) From {
            Path.Combine(BaseDir, "..", "..", "..", "..", "..", "bin", "Backend", "win-x64", BackendExeName),
            Path.Combine(BaseDir, BackendExeName),
            Path.Combine(BaseDir, "Backend", BackendExeName),
            Path.Combine(BaseDir, "OmniMixPlayer.Backend", BackendExeName),
            Path.Combine(BaseDir, "..", "Backend", BackendExeName),
            Path.Combine(BaseDir, "..", "OmniMixPlayer.Backend", BackendExeName),
            Path.Combine(BaseDir, "..", "bin", "Backend", "win-x64", BackendExeName),
            Path.Combine(BaseDir, "..", "..", "..", "..", "..", "bin", "Backend", "win-x64", BackendExeName)
        }
    End Function

    Private Function IsBackendProcessRunning() As Boolean
        Try
            Return Process.GetProcessesByName("OmniMixPlayer.Backend").Any()
        Catch
            Return True
        End Try
    End Function

End Module
