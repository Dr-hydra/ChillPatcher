Imports System.Diagnostics

Public Enum OmniMixServiceState
    NotInstalled
    Installed
    Running
End Enum

Public Module OmniMixPlatformService

    Public Const ServiceName As String = "OmniMixPlayerBackend"

    Public Async Function GetServiceStateAsync() As Task(Of OmniMixServiceState)
        Dim Result = Await RunProcessAsync("sc.exe", New List(Of String) From {"query", ServiceName})
        If Result.ExitCode <> 0 Then Return OmniMixServiceState.NotInstalled

        Dim Output = (Result.StdOut & vbCrLf & Result.StdErr).ToUpperInvariant()
        If Output.Contains("RUNNING") OrElse Output.Contains("STATE              : 4") OrElse Output.Contains("STATE              : 4 ") Then
            Return OmniMixServiceState.Running
        End If
        Return OmniMixServiceState.Installed
    End Function

    Public Async Function IsServiceAutoStartAsync() As Task(Of Boolean)
        Dim Result = Await RunProcessAsync("sc.exe", New List(Of String) From {"qc", ServiceName})
        If Result.ExitCode <> 0 Then Return False

        Dim Output = (Result.StdOut & vbCrLf & Result.StdErr).ToUpperInvariant()
        Return Output.Contains("AUTO_START") OrElse Output.Contains("START_TYPE         : 2")
    End Function

    Public Async Function GetServiceBinaryPathAsync() As Task(Of String)
        Dim Result = Await RunProcessAsync("sc.exe", New List(Of String) From {"qc", ServiceName})
        If Result.ExitCode <> 0 Then Return ""

        For Each RawLine In Result.StdOut.Split({vbCrLf, vbLf}, StringSplitOptions.None)
            Dim Line = RawLine.Trim()
            If Line.ToUpperInvariant().Contains("BINARY_PATH_NAME") Then
                Dim Index = Line.IndexOf(":"c)
                If Index >= 0 AndAlso Index < Line.Length - 1 Then
                    Return Line.Substring(Index + 1).Trim().Trim(""""c)
                End If
            End If
        Next
        Return ""
    End Function

    Public Async Function InstallServiceAsync() As Task(Of Boolean)
        Dim BackendPath = OmniMixBackendManager.FindBackendExe()
        If String.IsNullOrWhiteSpace(BackendPath) Then Return False

        Await RunElevatedAsync("sc.exe", New List(Of String) From {"stop", ServiceName})
        Await RunElevatedAsync("sc.exe", New List(Of String) From {"delete", ServiceName})

        Dim Created = Await RunElevatedAsync("sc.exe", New List(Of String) From {
            "create",
            ServiceName,
            "binPath=",
            """" & BackendPath & """",
            "start=",
            "demand"
        })
        If Not Created Then Return False

        Await RunElevatedAsync("sc.exe", New List(Of String) From {
            "sdset",
            ServiceName,
            "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;CCDCRPWP;;;AU)"
        })
        Return True
    End Function

    Public Async Function UpdateServiceBinaryPathAsync(Optional BackendPath As String = "") As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(BackendPath) Then BackendPath = OmniMixBackendManager.FindBackendExe()
        If String.IsNullOrWhiteSpace(BackendPath) Then Return False

        Dim State = Await GetServiceStateAsync()
        If State = OmniMixServiceState.NotInstalled Then Return False

        Dim CurrentPath = Await GetServiceBinaryPathAsync()
        If ArePathsEqual(CurrentPath, BackendPath) Then Return True

        Dim Args As New List(Of String) From {
            "config",
            ServiceName,
            "binPath=",
            """" & BackendPath & """"
        }

        Dim Normal = Await RunProcessAsync("sc.exe", Args)
        If Normal.ExitCode = 0 Then Return True
        Return Await RunElevatedAsync("sc.exe", Args)
    End Function

    Public Async Function UninstallServiceAsync() As Task(Of Boolean)
        Await RunElevatedAsync("sc.exe", New List(Of String) From {"stop", ServiceName})
        Return Await RunElevatedAsync("sc.exe", New List(Of String) From {"delete", ServiceName})
    End Function

    Public Async Function StartServiceAsync() As Task(Of Boolean)
        If Not Await UpdateServiceBinaryPathAsync() Then Return False
        Dim Normal = Await RunProcessAsync("sc.exe", New List(Of String) From {"start", ServiceName})
        If Normal.ExitCode = 0 Then Return True
        Return Await RunElevatedAsync("sc.exe", New List(Of String) From {"start", ServiceName})
    End Function

    Public Async Function StopServiceAsync() As Task(Of Boolean)
        Dim Normal = Await RunProcessAsync("sc.exe", New List(Of String) From {"stop", ServiceName})
        If Normal.ExitCode = 0 Then Return True
        Return Await RunElevatedAsync("sc.exe", New List(Of String) From {"stop", ServiceName})
    End Function

    Public Async Function SetServiceAutoStartAsync(AutoStart As Boolean) As Task(Of Boolean)
        Dim StartType = If(AutoStart, "auto", "demand")
        Dim Normal = Await RunProcessAsync("sc.exe", New List(Of String) From {"config", ServiceName, "start=", StartType})
        If Normal.ExitCode = 0 Then Return True
        Return Await RunElevatedAsync("sc.exe", New List(Of String) From {"config", ServiceName, "start=", StartType})
    End Function

    Public Function ArePathsEqual(PathA As String, PathB As String) As Boolean
        If String.IsNullOrWhiteSpace(PathA) OrElse String.IsNullOrWhiteSpace(PathB) Then Return False
        Try
            PathA = IO.Path.GetFullPath(PathA.Trim().Trim(""""c))
            PathB = IO.Path.GetFullPath(PathB.Trim().Trim(""""c))
        Catch
        End Try
        Return String.Equals(PathA.Replace("/"c, "\"c), PathB.Replace("/"c, "\"c), StringComparison.OrdinalIgnoreCase)
    End Function

    Private Async Function RunElevatedAsync(FileName As String, Arguments As List(Of String)) As Task(Of Boolean)
        Dim Command = "$p = Start-Process -FilePath '" & EscapePowerShell(FileName) & "' -ArgumentList @(" &
            String.Join(",", Arguments.Select(Function(Arg) "'" & EscapePowerShell(Arg) & "'")) &
            ") -Verb RunAs -Wait -WindowStyle Hidden -PassThru; exit $p.ExitCode"
        Dim Result = Await RunProcessAsync("powershell.exe", New List(Of String) From {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            Command
        })
        Return Result.ExitCode = 0
    End Function

    Private Async Function RunProcessAsync(FileName As String, Arguments As List(Of String)) As Task(Of ProcessResult)
        Return Await Task.Run(Function()
                                  Try
                                      Dim StartInfo As New ProcessStartInfo With {
                                          .FileName = FileName,
                                          .UseShellExecute = False,
                                          .CreateNoWindow = True,
                                          .WindowStyle = ProcessWindowStyle.Hidden,
                                          .RedirectStandardOutput = True,
                                          .RedirectStandardError = True
                                      }
                                      For Each Arg In Arguments
                                          StartInfo.ArgumentList.Add(Arg)
                                      Next

                                      Using Proc = Process.Start(StartInfo)
                                          Dim StdOut = Proc.StandardOutput.ReadToEnd()
                                          Dim StdErr = Proc.StandardError.ReadToEnd()
                                          Proc.WaitForExit()
                                          Return New ProcessResult With {
                                              .ExitCode = Proc.ExitCode,
                                              .StdOut = StdOut,
                                              .StdErr = StdErr
                                          }
                                      End Using
                                  Catch Ex As Exception
                                      Return New ProcessResult With {
                                          .ExitCode = -1,
                                          .StdOut = "",
                                          .StdErr = Ex.Message
                                      }
                                  End Try
                              End Function)
    End Function

    Private Function EscapePowerShell(Value As String) As String
        Return If(Value, "").Replace("'", "''")
    End Function

    Private Class ProcessResult
        Public Property ExitCode As Integer
        Public Property StdOut As String = ""
        Public Property StdErr As String = ""
    End Class

End Module
