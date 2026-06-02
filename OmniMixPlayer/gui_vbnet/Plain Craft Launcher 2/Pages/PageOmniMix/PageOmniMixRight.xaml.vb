Imports System.Globalization
Imports System.Text.Json

Public Class PageOmniMixRight

    Private PageKey As String = "Home"
    Private InitialStatusText As String = ""
    Private HasCheckedBackend As Boolean = False
    Private CurrentBaseUrl As String = ""
    Private CurrentModuleId As String = ""
    Private CurrentUiKind As String = "default"
    Private CurrentLinkId As String = ""
    Private ActiveInstanceId As String = ""
    Private CanControlActiveInstance As Boolean = False
    Private IsShowingHistory As Boolean = False
    Private IsUpdatingPlaybackUi As Boolean = False
    Private CurrentModulesPane As String = "launchpad"
    Private PreviousModulesPaneForDetail As String = "launchpad"
    Private CurrentLibraryPane As String = ""
    Private CurrentLibraryPlaylist As OmniMixPlaylistData = New OmniMixPlaylistData()
    Private CurrentModules As List(Of OmniMixModuleInfo) = New List(Of OmniMixModuleInfo)
    Private ExpandedModuleKey As String = ""
    Private ExpandedModuleTree As OmniMixRawNodeData = Nothing
    Private ExpandedModuleError As String = ""
    Private ExpandedModuleLoading As Boolean = False
    Private ExpandedModuleTitle As String = ""
    Private ModuleUiRequestSerial As Integer = 0
    Private CurrentSettingsPane As String = "config"
    Private CurrentEqualizerInstanceId As String = ""
    Private CurrentEqualizerState As OmniMixEqualizerStateInfo = Nothing
    Private CurrentEqualizerPresets As Dictionary(Of String, OmniMixEqualizerStateInfo) = New Dictionary(Of String, OmniMixEqualizerStateInfo)
    Private DeploymentLogs As List(Of String) = New List(Of String)
    Private ReadOnly WsClient As New OmniMixWsClient()
    Private ReadOnly ExpandedLibraryAlbums As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly PlaybackAutoRefreshTimer As New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(1)}
    Private IsAutoRefreshingPlayback As Boolean = False
    Private CurrentQueueRenderKey As String = ""
    Private Const IconMoveUp As String = "M512 170.667 192 490.667h192v341.333h128V490.667h192L512 170.667z"
    Private Const IconMoveDown As String = "M512 853.333 832 533.333H640V192H512v341.333H320L512 853.333z"
    Private Const IconPlay As String = "M352 224v672l480-336-480-336z"
    Private Const IconPlus As String = "M448 128h128v320h320v128H576v320H448V576H128V448h320z"
    Private Const IconExpandDown As String = "M192 352h640L512 704z"
    Private Const IconExpandUp As String = "M192 672h640L512 320z"

    Private Class LibraryQueueGroupPayload
        Public Property GroupId As String = ""
        Public Property Name As String = ""
        Public Property Uuids As List(Of String) = New List(Of String)
    End Class

    Private Class LibraryAlbumGroup
        Public Property GroupId As String = ""
        Public Property Name As String = ""
        Public Property ModuleId As String = ""
        Public Property CoverPath As String = ""
        Public Property Songs As List(Of OmniMixSongInfo) = New List(Of OmniMixSongInfo)
    End Class

    Public Shared Function Create(PageKey As String) As PageOmniMixRight
        Dim Page As New PageOmniMixRight
        Page.Configure(PageKey)
        Return Page
    End Function

    Public Sub Configure(PageKey As String)
        Me.PageKey = PageKey
        GridHome.Visibility = If(PageKey = "Home", Visibility.Visible, Visibility.Collapsed)
        CardLibrary.Visibility = If(PageKey = "Library", Visibility.Visible, Visibility.Collapsed)
        CardModules.Visibility = If(PageKey = "Modules", Visibility.Visible, Visibility.Collapsed)
        CardSettings.Visibility = If(PageKey = "Settings", Visibility.Visible, Visibility.Collapsed)
        CardAbout.Visibility = If(PageKey = "About", Visibility.Visible, Visibility.Collapsed)
        CardModuleUi.Visibility = Visibility.Collapsed
        Select Case PageKey
            Case "Home"
                LabTitle.Text = "播放"
                LabSubtitle.Text = "左侧控制当前播放，右侧管理播放队列。"
                LabStatus.Text = "正在等待后端状态。"
            Case "Library"
                LabTitle.Text = "音乐库"
                LabSubtitle.Text = "按来源浏览后端聚合出的音乐库。"
                LabStatus.Text = "接口：/api/playlist。"
            Case "Modules"
                LabTitle.Text = "插件"
                LabSubtitle.Text = "在 Mod 与游戏集成之间切换，并管理音乐源模块。"
                LabStatus.Text = "接口：/api/modules、模块设置 UI、快捷入口、播放实例和 WebSocket UI 事件。"
            Case "Settings"
                LabTitle.Text = "设置"
                LabSubtitle.Text = "后端配置、播放实例档案和 GUI 本地偏好。"
                LabStatus.Text = "下一步按 Flutter 设置页继续移植具体配置项。"
            Case "About"
                LabTitle.Text = "关于"
                LabSubtitle.Text = "展示 OmniMix Player、模块 SDK、PCL UI 框架来源与开源声明。"
                LabStatus.Text = "OmniMix Player 是音乐源聚合桌面端；PCL 仅作为 UI 框架与视觉风格来源。"
            Case Else
                LabTitle.Text = "OmniMix"
                LabSubtitle.Text = "未知页面。"
                LabStatus.Text = "页面键：" & PageKey
        End Select
        InitialStatusText = LabStatus.Text
    End Sub

    Public Sub SetLibraryPane(Pane As String)
        CurrentLibraryPane = If(Pane, "")
        If PageKey <> "Library" Then Return

        CardLibrary.Title = "音乐库"
        LabPlaylistSourcesSummary.Visibility = Visibility.Collapsed
        PanPlaylistSources.Visibility = Visibility.Collapsed
        PanLibraryList.Visibility = Visibility.Visible
        LabLibrarySummary.Visibility = Visibility.Visible
        RenderLibrary()
    End Sub

    Public Sub SetModulesPane(ShowGameIntegration As Boolean)
        SetModulesPane(If(ShowGameIntegration, "game", "mod"))
    End Sub

    Public Async Sub SetModulesPane(Pane As String)
        If PageKey <> "Modules" Then Return
        If CardModuleUi.Visibility = Visibility.Visible Then CollapseModuleUi(False)
        Dim NormalizedPane = If(Pane, "launchpad").Trim().ToLowerInvariant()
        If NormalizedPane <> "game" AndAlso NormalizedPane <> "mod" Then NormalizedPane = "launchpad"
        If String.Equals(CurrentModulesPane, NormalizedPane, StringComparison.OrdinalIgnoreCase) Then
            UpdatePluginPaneVisibility()
            If CurrentModules.Count > 0 Then
                If CurrentModulesPane = "launchpad" Then RenderLaunchpad(CurrentModules)
                If CurrentModulesPane = "mod" Then RenderModules(CurrentModules)
            End If
            Return
        End If

        CurrentModulesPane = NormalizedPane
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshModulesAsync(CurrentBaseUrl)
        End If
    End Sub

    Public Async Sub SetSettingsPane(Pane As String)
        CurrentSettingsPane = If(String.IsNullOrWhiteSpace(Pane), "config", Pane)
        If PageKey <> "Settings" Then Return

        Dim ShowConfig = String.Equals(CurrentSettingsPane, "config", StringComparison.OrdinalIgnoreCase)
        Dim ShowPersonalization = String.Equals(CurrentSettingsPane, "personalization", StringComparison.OrdinalIgnoreCase)
        Dim ShowInstances = String.Equals(CurrentSettingsPane, "instances", StringComparison.OrdinalIgnoreCase)
        Dim ShowArchives = String.Equals(CurrentSettingsPane, "archives", StringComparison.OrdinalIgnoreCase)
        Dim ShowEqualizer = String.Equals(CurrentSettingsPane, "equalizer", StringComparison.OrdinalIgnoreCase)

        If ShowInstances Then
            CardSettings.Title = "设置 - 播放实例"
        ElseIf ShowArchives Then
            CardSettings.Title = "设置 - 归档"
        ElseIf ShowEqualizer Then
            CardSettings.Title = "设置 - 均衡器"
        ElseIf ShowPersonalization Then
            CardSettings.Title = "设置 - 个性化"
        Else
            CardSettings.Title = "设置 - 后端配置"
        End If
        LabSettingsSummary.Visibility = If(ShowConfig, Visibility.Visible, Visibility.Collapsed)
        PanSettingsConfig.Visibility = If(ShowConfig, Visibility.Visible, Visibility.Collapsed)
        PanSettingsChecks.Visibility = If(ShowConfig, Visibility.Visible, Visibility.Collapsed)
        PanSettingsConfigButtons.Visibility = If(ShowConfig, Visibility.Visible, Visibility.Collapsed)
        PanSettingsService.Visibility = Visibility.Collapsed
        PanSettingsPersonalization.Visibility = If(ShowPersonalization, Visibility.Visible, Visibility.Collapsed)
        PanSettingsEqualizer.Visibility = If(ShowEqualizer, Visibility.Visible, Visibility.Collapsed)
        LabInstanceStats.Visibility = If(ShowInstances, Visibility.Visible, Visibility.Collapsed)
        PanSettingsInstances.Visibility = If(ShowInstances, Visibility.Visible, Visibility.Collapsed)
        LabArchiveSummary.Visibility = If(ShowArchives, Visibility.Visible, Visibility.Collapsed)
        BtnArchivesRefresh.Visibility = If(ShowArchives, Visibility.Visible, Visibility.Collapsed)
        PanSettingsArchives.Visibility = If(ShowArchives, Visibility.Visible, Visibility.Collapsed)

        If ShowEqualizer Then
            If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
                LabEqualizerSummary.Text = "等待连接后端后读取均衡器。"
                PanEqualizerPresets.Children.Clear()
                PanEqualizerPoints.Children.Clear()
            Else
                Await RefreshEqualizerAsync(CurrentBaseUrl)
            End If
        ElseIf ShowConfig Then
            Await RefreshServiceStatusAsync()
        End If
    End Sub

    Private Async Sub PageOmniMixRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        BtnBackendPathBrowse.Logo = Logo.IconButtonOpen
        BtnBackendPathReset.Logo = Logo.IconButtonRefresh
        RemoveHandler PlaybackAutoRefreshTimer.Tick, AddressOf PlaybackAutoRefreshTimer_Tick
        AddHandler PlaybackAutoRefreshTimer.Tick, AddressOf PlaybackAutoRefreshTimer_Tick
        RemoveHandler WsClient.MessageReceived, AddressOf WsClient_MessageReceived
        AddHandler WsClient.MessageReceived, AddressOf WsClient_MessageReceived
        If HasCheckedBackend Then
            UpdatePlaybackAutoRefreshTimer()
            Return
        End If
        HasCheckedBackend = True
        Await RefreshBackendStatusAsync()
    End Sub

    Private Sub PageOmniMixRight_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        PlaybackAutoRefreshTimer.Stop()
        RemoveHandler WsClient.MessageReceived, AddressOf WsClient_MessageReceived
    End Sub

    Private Sub WsClient_MessageReceived(Message As String)
        If String.IsNullOrWhiteSpace(Message) Then Return

        Try
            Using Doc = JsonDocument.Parse(Message)
                Dim Root = Doc.RootElement
                Dim TypeElement As JsonElement
                If Not Root.TryGetProperty("type", TypeElement) OrElse TypeElement.GetString() <> "ui_push" Then Return

                Dim DataElement As JsonElement
                If Not Root.TryGetProperty("data", DataElement) Then Return

                Dim ModuleIdElement As JsonElement
                If Not DataElement.TryGetProperty("moduleId", ModuleIdElement) Then Return
                Dim ModuleId = ModuleIdElement.GetString()
                If String.IsNullOrWhiteSpace(ModuleId) Then Return

                Dim TreeElement As JsonElement
                If Not DataElement.TryGetProperty("tree", TreeElement) OrElse TreeElement.ValueKind = JsonValueKind.Null Then Return

                Dim Options As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
                Dim Tree = JsonSerializer.Deserialize(Of OmniMixRawNodeData)(TreeElement.GetRawText(), Options)
                If Tree Is Nothing Then Return

                RunInUi(Sub()
                            If CardModuleUi.Visibility <> Visibility.Visible Then Return
                            If Not String.Equals(CurrentModuleId, ModuleId, StringComparison.OrdinalIgnoreCase) Then Return
                            PanModuleUi.Children.Clear()
                            PanModuleUi.Children.Add(OmniMixRawNodeRenderer.Render(Tree, CurrentBaseUrl, AddressOf ModuleUiEvent_Dispatch))
                            LabModulesSummary.Text = "模块 UI 已更新：" & ExpandedModuleTitle
                            LabModuleUiSummary.Text = "模块 UI 已更新。"
                        End Sub)
            End Using
        Catch
        End Try
    End Sub

    Private Async Function RefreshBackendStatusAsync() As Task
        LoadState.Visibility = Visibility.Visible
        LoadState.State.LoadingState = MyLoading.MyLoadingState.Run
        LabStatus.Text = InitialStatusText & vbCrLf & vbCrLf & "正在发现或启动 OmniMix 后端..."
        FrmMain?.SetOmniMixConnectionStatus(False)

        Dim Status = Await OmniMixBackendManager.EnsureStartedAsync()
        If Status.IsOnline Then
            LoadState.State.LoadingState = MyLoading.MyLoadingState.Stop
            LoadState.Visibility = Visibility.Collapsed
            LabStatus.Text = InitialStatusText & vbCrLf & vbCrLf &
                "后端状态：在线" & vbCrLf &
                "连接地址：" & Status.BaseUrl & vbCrLf &
                "健康检查：/api/health 已通过" & vbCrLf &
                If(Status.StartedBackend, "启动方式：GUI 已自动启动后端", "启动方式：发现已有后端")
            CurrentBaseUrl = Status.BaseUrl
            FrmMain?.SetOmniMixConnectionStatus(True, Status.BaseUrl)
            If PageKey = "Home" Then Await RefreshPlaybackAsync(Status.BaseUrl)
            If PageKey = "Library" Then Await RefreshLibraryAsync(Status.BaseUrl)
            If PageKey = "Modules" Then Await RefreshModulesAsync(Status.BaseUrl)
            If PageKey = "Settings" Then Await RefreshSettingsAsync(Status.BaseUrl)
            UpdatePlaybackAutoRefreshTimer()
        Else
            LoadState.State.LoadingState = MyLoading.MyLoadingState.Error
            LabStatus.Text = InitialStatusText & vbCrLf & vbCrLf &
                "后端状态：离线" & vbCrLf &
                Status.Message & vbCrLf &
                "GUI 会继续运行，后续可以在这里重新尝试连接后端。"
            FrmMain?.SetOmniMixConnectionStatus(False)
            If PageKey = "Library" Then
                PanLibraryList.Children.Clear()
                PanPlaylistSources.Children.Clear()
                LabLibrarySummary.Text = "后端未连接，暂时无法读取曲库。"
                LabPlaylistSourcesSummary.Text = ""
                LabPlaylistSourcesSummary.Visibility = Visibility.Collapsed
                PanPlaylistSources.Visibility = Visibility.Collapsed
            End If
            If PageKey = "Modules" Then
                PanModulesList.Children.Clear()
                PanGameIntegrationList.Children.Clear()
                LabModulesSummary.Text = "后端未连接，暂时无法读取模块列表。"
            End If
            If PageKey = "Home" Then
                ActiveInstanceId = ""
                CanControlActiveInstance = False
                RenderPlayback(New List(Of OmniMixPlaybackInstanceInfo), Nothing)
                LabPlaybackSummary.Text = "后端未连接，暂时无法读取播放实例。"
                RenderQueueItems(New List(Of OmniMixQueueItemInfo), IsShowingHistory)
                LabQueueSummary.Text = ""
                LabQueueSummary.Visibility = Visibility.Collapsed
            End If
            If PageKey = "Settings" Then
                LabSettingsSummary.Text = "后端未连接，暂时无法读取或保存配置。"
                Await RefreshServiceStatusAsync()
                LabInstanceStats.Text = "实例统计：--"
                PanSettingsInstances.Children.Clear()
                LabArchiveSummary.Text = "归档：后端未连接。"
                PanSettingsArchives.Children.Clear()
                LabEqualizerSummary.Text = "均衡器：后端未连接。"
                PanEqualizerPresets.Children.Clear()
                PanEqualizerPoints.Children.Clear()
                CurrentEqualizerInstanceId = ""
                CurrentEqualizerState = Nothing
                CurrentEqualizerPresets.Clear()
            End If
            UpdatePlaybackAutoRefreshTimer()
        End If
    End Function

    Private Async Function RefreshSettingsAsync(BaseUrl As String) As Task
        If PageKey <> "Settings" Then Return

        LabSettingsSummary.Text = "正在读取后端配置..."
        PanSettingsInstances.Children.Clear()

        Try
            Dim Config = Await OmniMixApiClient.GetConfigAsync(BaseUrl)
            TxtBackendPort.Text = ConfigString(Config, "backend_port", "17890")
            TxtBackendBind.Text = ConfigString(Config, "backend_bind", "127.0.0.1")
            Dim ConfiguredBackendPath = OmniMixBackendManager.GetConfiguredBackendPath()
            Dim DefaultBackendPath = OmniMixBackendManager.FindDefaultBackendExe()
            TxtBackendPath.HintText = If(String.IsNullOrWhiteSpace(DefaultBackendPath), "OmniMixPlayer.Backend.exe", "默认：" & DefaultBackendPath)
            If Not String.IsNullOrWhiteSpace(ConfiguredBackendPath) AndAlso
               Not OmniMixPlatformService.ArePathsEqual(ConfiguredBackendPath, DefaultBackendPath) Then
                TxtBackendPath.Text = ConfiguredBackendPath
            Else
                TxtBackendPath.Text = ""
                If Not String.IsNullOrWhiteSpace(ConfiguredBackendPath) Then OmniMixBackendManager.SetConfiguredBackendPath("")
            End If
            BtnBackendPathBrowse.Logo = Logo.IconButtonOpen
            BtnBackendPathReset.Logo = Logo.IconButtonRefresh
            CheckAutostart.Checked = ConfigBoolean(Config, "autostart", False)
            CheckMinimizeToTray.Checked = ConfigBoolean(Config, "minimize_to_tray", True)
            Await RefreshServiceStatusAsync()

            Dim Stats = Await OmniMixApiClient.GetInstanceStatsAsync(BaseUrl)
            Dim Instances = Await OmniMixApiClient.GetInstancesAsync(BaseUrl)
            Dim Archives = Await OmniMixApiClient.GetArchivesAsync(BaseUrl)
            RenderSettingsStats(Stats, Instances, ConfigString(Config, "active_instance", ""))
            RenderSettingsArchives(Archives, Instances, ConfigString(Config, "active_instance", ""))
            LabSettingsSummary.Text = "已读取后端配置。保存后会写入全局配置文件；端口和绑定地址通常需要重启后端后生效。"
            SetSettingsPane(CurrentSettingsPane)
        Catch Ex As Exception
            LabSettingsSummary.Text = "设置读取失败：" & Ex.Message
            LabInstanceStats.Text = "实例统计：读取失败"
            LabArchiveSummary.Text = "归档：读取失败"
        End Try
    End Function

    Private Sub RenderSettingsStats(Stats As OmniMixInstanceStatsInfo, Instances As List(Of OmniMixPlaybackInstanceInfo), ActiveInstanceConfigId As String)
        Stats = If(Stats, New OmniMixInstanceStatsInfo)
        Instances = If(Instances, New List(Of OmniMixPlaybackInstanceInfo))
        LabInstanceStats.Text =
            $"实例统计：{Stats.InstanceCount} 个实例，{Stats.AttachedAudioClients} 个在线音频端，{Stats.ControllerClients} 个控制端，队列合计 {Stats.TotalQueueItems} 首。"

        PanSettingsInstances.Children.Clear()
        If Instances.Count = 0 Then
            PanSettingsInstances.Children.Add(New MyListItem With {
                .Title = "暂无播放实例",
                .Info = "后端在线，但还没有音频实例连接。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        For Each Instance In Instances.OrderBy(Function(Item) Item.Id)
            Dim IsActive = String.Equals(Instance.Id, ActiveInstanceConfigId, StringComparison.OrdinalIgnoreCase)
            Dim InfoParts As New List(Of String)
            If IsActive Then InfoParts.Add("当前")
            InfoParts.Add(If(Instance.Attached, "在线", "离线"))
            InfoParts.Add(If(Instance.IsServerManaged, "后端控制", "客户端控制"))
            If Not String.IsNullOrWhiteSpace(Instance.GameName) Then InfoParts.Add(Instance.GameName)
            If Not String.IsNullOrWhiteSpace(Instance.ModId) Then InfoParts.Add("Mod " & Instance.ModId)
            If Instance.Attached AndAlso Not Instance.SharedMemoryReady Then InfoParts.Add("共享内存未就绪")
            InfoParts.Add("音量 " & CInt(Math.Round(Instance.Volume * 100)) & "%")
            InfoParts.Add("队列 " & Instance.QueueCount)

            Dim Item As New MyListItem With {
                .Title = If(IsActive, "✓ ", "") & NonEmpty(Instance.GameName, NonEmpty(Instance.Id, Instance.ClientId)),
                .Info = String.Join(" · ", InfoParts),
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Instance
            }

            Dim Buttons As New List(Of MyIconButton)
            Dim SelectButton As New MyIconButton With {
                .Logo = Logo.IconButtonOpen,
                .LogoScale = 1.05,
                .ToolTip = If(IsActive, "当前实例", "设为当前实例"),
                .Tag = Instance,
                .IsEnabled = Not IsActive
            }
            AddHandler SelectButton.Click, AddressOf SettingsInstanceSelectButton_Click
            Buttons.Add(SelectButton)

            Dim EditButton As New MyIconButton With {
                .Logo = Logo.IconButtonEdit,
                .LogoScale = 0.95,
                .ToolTip = "编辑实例元数据",
                .Tag = Instance
            }
            AddHandler EditButton.Click, AddressOf SettingsInstanceMetaButton_Click
            Buttons.Add(EditButton)

            Dim ArchiveButton As New MyIconButton With {
                .Logo = Logo.IconButtonSave,
                .LogoScale = 0.95,
                .ToolTip = "保存实例归档",
                .Tag = Instance
            }
            AddHandler ArchiveButton.Click, AddressOf SettingsInstanceArchiveButton_Click
            Buttons.Add(ArchiveButton)

            If Not Instance.Attached Then
                Dim DeleteButton As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .LogoScale = 0.95,
                    .ToolTip = "删除离线实例",
                    .Tag = Instance
                }
                AddHandler DeleteButton.Click, AddressOf SettingsInstanceDeleteButton_Click
                Buttons.Add(DeleteButton)
            End If

            Item.Buttons = Buttons
            PanSettingsInstances.Children.Add(Item)
        Next
    End Sub

    Private Sub RenderSettingsArchives(Archives As List(Of OmniMixArchiveInfo), Instances As List(Of OmniMixPlaybackInstanceInfo), ActiveInstanceConfigId As String)
        Archives = If(Archives, New List(Of OmniMixArchiveInfo))
        Instances = If(Instances, New List(Of OmniMixPlaybackInstanceInfo))

        PanSettingsArchives.Children.Clear()
        LabArchiveSummary.Text = $"归档：{Archives.Count} 个已保存实例归档。"

        If Archives.Count = 0 Then
            PanSettingsArchives.Children.Add(New MyListItem With {
                .Title = "暂无归档",
                .Info = "可在上方实例列表中把当前播放实例保存为归档。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        For Each Archive In Archives.OrderByDescending(Function(Item) ArchiveTicks(Item.ArchivedAt)).ThenBy(Function(Item) Item.InstanceId)
            Dim IsOnline = Instances.Any(Function(Instance) String.Equals(Instance.Id, Archive.InstanceId, StringComparison.OrdinalIgnoreCase) AndAlso Instance.Attached)
            Dim InfoParts As New List(Of String)
            If Not String.IsNullOrWhiteSpace(Archive.Mode) Then InfoParts.Add(Archive.Mode)
            If Not String.IsNullOrWhiteSpace(Archive.ModId) Then InfoParts.Add(Archive.ModId)
            InfoParts.Add("实例 " & NonEmpty(Archive.InstanceId, "--"))
            If IsOnline Then InfoParts.Add("在线，暂不删除")
            If Not String.IsNullOrWhiteSpace(Archive.ArchivedAt) Then InfoParts.Add(FormatArchiveTime(Archive.ArchivedAt))

            Dim Item As New MyListItem With {
                .Title = NonEmpty(Archive.DisplayName, Archive.InstanceId),
                .Info = String.Join(" · ", InfoParts),
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Archive
            }

            Dim Buttons As New List(Of MyIconButton)
            Dim InheritButton As New MyIconButton With {
                .Logo = Logo.IconButtonOpen,
                .LogoScale = 1.05,
                .ToolTip = "应用到实例",
                .Tag = Tuple.Create(Archive, ActiveInstanceConfigId)
            }
            AddHandler InheritButton.Click, AddressOf ArchiveInheritButton_Click
            Buttons.Add(InheritButton)

            Dim RenameButton As New MyIconButton With {
                .Logo = Logo.IconButtonEdit,
                .LogoScale = 0.95,
                .ToolTip = "重命名归档",
                .Tag = Archive
            }
            AddHandler RenameButton.Click, AddressOf ArchiveRenameButton_Click
            Buttons.Add(RenameButton)

            If Not IsOnline Then
                Dim DeleteButton As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .LogoScale = 0.95,
                    .ToolTip = "删除归档",
                    .Tag = Archive
                }
                AddHandler DeleteButton.Click, AddressOf ArchiveDeleteButton_Click
                Buttons.Add(DeleteButton)
            End If

            Item.Buttons = Buttons
            PanSettingsArchives.Children.Add(Item)
        Next
    End Sub

    Private Async Sub BtnSettingsRefresh_Click(sender As Object, e As EventArgs) Handles BtnSettingsRefresh.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshSettingsAsync(CurrentBaseUrl)
        End If
    End Sub

    Private Async Sub BtnSettingsSave_Click(sender As Object, e As EventArgs) Handles BtnSettingsSave.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Dim Port = TxtBackendPort.Text.Trim()
        Dim ParsedPort As Integer
        If Not Integer.TryParse(Port, ParsedPort) OrElse ParsedPort <= 0 OrElse ParsedPort >= 65536 Then
            LabSettingsSummary.Text = "端口无效，请输入 1 到 65535 之间的数字。"
            Return
        End If

        Dim Bind = TxtBackendBind.Text.Trim()
        Dim BackendPath = TxtBackendPath.Text.Trim()
        If Not String.IsNullOrWhiteSpace(BackendPath) Then
            If Not File.Exists(BackendPath) Then
                LabSettingsSummary.Text = "后端程序不存在：" & BackendPath
                Return
            End If
            If Not String.Equals(Path.GetFileName(BackendPath), "OmniMixPlayer.Backend.exe", StringComparison.OrdinalIgnoreCase) Then
                LabSettingsSummary.Text = "请选择 OmniMixPlayer.Backend.exe。"
                Return
            End If
        End If
        If String.IsNullOrWhiteSpace(Bind) Then
            LabSettingsSummary.Text = "绑定地址不能为空。"
            Return
        End If

        Try
            OmniMixBackendManager.SetConfiguredBackendPath(BackendPath)
            Dim Updates As New Dictionary(Of String, Object) From {
                {"backend_port", Port},
                {"backend_bind", Bind},
                {"autostart", CheckAutostart.Checked},
                {"minimize_to_tray", CheckMinimizeToTray.Checked}
            }
            Await OmniMixApiClient.PutConfigRawAsync(CurrentBaseUrl, Updates)
            Await OmniMixApiClient.SaveConfigAsync(CurrentBaseUrl)
            LabSettingsSummary.Text = "配置已保存。端口和绑定地址变更会在后端重启后生效。"
        Catch Ex As Exception
            LabSettingsSummary.Text = "配置保存失败：" & Ex.Message
        End Try
    End Sub

    Private Sub BtnBackendPathBrowse_Click(sender As Object, e As EventArgs) Handles BtnBackendPathBrowse.Click
        Using Dialog As New System.Windows.Forms.OpenFileDialog()
            Dialog.Title = "选择 OmniMixPlayer.Backend.exe"
            Dialog.Filter = "OmniMixPlayer.Backend.exe|OmniMixPlayer.Backend.exe|可执行文件|*.exe|所有文件|*.*"
            Dialog.CheckFileExists = True
            Dim CurrentPath = TxtBackendPath.Text.Trim()
            If File.Exists(CurrentPath) Then
                Dialog.FileName = CurrentPath
                Dialog.InitialDirectory = Path.GetDirectoryName(CurrentPath)
            Else
                Dim DefaultPath = OmniMixBackendManager.FindDefaultBackendExe()
                If File.Exists(DefaultPath) Then Dialog.InitialDirectory = Path.GetDirectoryName(DefaultPath)
            End If
            If Dialog.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
            TxtBackendPath.Text = Dialog.FileName
        End Using
    End Sub

    Private Sub BtnBackendPathReset_Click(sender As Object, e As EventArgs) Handles BtnBackendPathReset.Click
        OmniMixBackendManager.SetConfiguredBackendPath("")
        Dim DefaultBackendPath = OmniMixBackendManager.FindDefaultBackendExe()
        TxtBackendPath.HintText = If(String.IsNullOrWhiteSpace(DefaultBackendPath), "OmniMixPlayer.Backend.exe", "默认：" & DefaultBackendPath)
        TxtBackendPath.Text = ""
        LabSettingsSummary.Text = "已恢复为默认构建后端。"
    End Sub

    Private Async Sub BtnBackendReconnect_Click(sender As Object, e As EventArgs) Handles BtnBackendReconnect.Click
        CurrentBaseUrl = ""
        Await RefreshBackendStatusAsync()
    End Sub

    Private Async Sub BtnBackendStop_Click(sender As Object, e As EventArgs) Handles BtnBackendStop.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            LabSettingsSummary.Text = "后端未连接，无需停止。"
            Return
        End If
        If MyMsgBox("确定要停止 OmniMix 后端吗？停止后曲库、播放控制和模块 UI 会暂时不可用，可稍后点击「启动/重连」恢复。", "停止后端", "停止", "取消", IsWarn:=True) <> 1 Then Return

        Try
            Dim StoppedUrl = CurrentBaseUrl
            LabSettingsSummary.Text = "正在停止 OmniMix 后端..."
            Await OmniMixApiClient.StopBackendAsync(StoppedUrl)
            CurrentBaseUrl = ""
            HasCheckedBackend = False
            LoadState.Visibility = Visibility.Visible
            LoadState.State.LoadingState = MyLoading.MyLoadingState.Error
            LabStatus.Text = InitialStatusText & vbCrLf & vbCrLf &
                "后端状态：已停止" & vbCrLf &
                "可在设置页点击「启动/重连」重新启动或发现后端。"
            FrmMain?.SetOmniMixConnectionStatus(False)
            LabSettingsSummary.Text = "后端已停止。"
            LabInstanceStats.Text = "实例统计：--"
            PanSettingsInstances.Children.Clear()
            LabArchiveSummary.Text = "归档：后端已停止。"
            PanSettingsArchives.Children.Clear()
            LabEqualizerSummary.Text = "均衡器：后端已停止。"
            PanEqualizerPresets.Children.Clear()
            PanEqualizerPoints.Children.Clear()
            CurrentEqualizerInstanceId = ""
            CurrentEqualizerState = Nothing
            CurrentEqualizerPresets.Clear()
            Hint("OmniMix 后端已停止。", HintType.Green)
        Catch Ex As Exception
            LabSettingsSummary.Text = "停止后端失败：" & Ex.Message
            Hint("停止 OmniMix 后端失败：" & Ex.Message, HintType.Red)
        End Try
    End Sub

    Private Async Function RefreshServiceStatusAsync() As Task
        If PageKey <> "Settings" Then Return

        Try
            LabServiceSummary.Text = "后端服务：正在读取..."
            Dim State = Await OmniMixPlatformService.GetServiceStateAsync()
            Dim AutoStart = Await OmniMixPlatformService.IsServiceAutoStartAsync()
            Dim BackendPath = OmniMixBackendManager.FindBackendExe()
            Dim RegisteredPath = If(State = OmniMixServiceState.NotInstalled, "", Await OmniMixPlatformService.GetServiceBinaryPathAsync())

            CheckServiceAutoStart.Checked = AutoStart

            BtnServiceInstall.Visibility = If(State = OmniMixServiceState.NotInstalled, Visibility.Visible, Visibility.Collapsed)
            BtnServiceUninstall.Visibility = If(State = OmniMixServiceState.NotInstalled, Visibility.Collapsed, Visibility.Visible)
            BtnServiceStart.Visibility = If(State = OmniMixServiceState.Installed, Visibility.Visible, Visibility.Collapsed)
            BtnServiceStop.Visibility = If(State = OmniMixServiceState.Running, Visibility.Visible, Visibility.Collapsed)
            BtnServiceToggleAutoStart.IsEnabled = State <> OmniMixServiceState.NotInstalled

            Dim StateText = GetServiceStateText(State)
            Dim AutoText = If(State = OmniMixServiceState.NotInstalled, "自启动：--", If(AutoStart, "自启动：已开启", "自启动：手动启动"))
            Dim PathText As String
            If State = OmniMixServiceState.NotInstalled Then
                PathText = If(String.IsNullOrWhiteSpace(BackendPath), "未找到后端 exe，暂不能安装服务。", "可安装后端服务：" & BackendPath)
            ElseIf String.IsNullOrWhiteSpace(RegisteredPath) Then
                PathText = "已安装，但未能读取服务路径。"
            ElseIf Not String.IsNullOrWhiteSpace(BackendPath) AndAlso Not OmniMixPlatformService.ArePathsEqual(BackendPath, RegisteredPath) Then
                PathText = "服务路径可能已过期：" & RegisteredPath
            Else
                PathText = "服务路径：" & RegisteredPath
            End If

            LabServiceSummary.Text = "后端服务：" & StateText & " · " & AutoText & vbCrLf & PathText
        Catch Ex As Exception
            LabServiceSummary.Text = "后端服务状态读取失败：" & Ex.Message
        End Try
    End Function

    Private Async Sub BtnServiceRefresh_Click(sender As Object, e As EventArgs) Handles BtnServiceRefresh.Click
        Await RefreshServiceStatusAsync()
    End Sub

    Private Async Sub BtnServiceInstall_Click(sender As Object, e As EventArgs) Handles BtnServiceInstall.Click
        Dim BackendPath = OmniMixBackendManager.FindBackendExe()
        If String.IsNullOrWhiteSpace(BackendPath) Then
            LabServiceSummary.Text = "未找到 OmniMixPlayer.Backend.exe，无法安装服务。"
            Return
        End If
        If MyMsgBox("将把 OmniMix 后端安装为 Windows 服务。这个操作可能会弹出 UAC 提权窗口。", "安装后端服务", "安装", "取消") <> 1 Then Return

        LabServiceSummary.Text = "正在安装后端服务..."
        Dim Success = Await OmniMixPlatformService.InstallServiceAsync()
        Hint(If(Success, "后端服务已安装。", "后端服务安装失败。"), If(Success, HintType.Green, HintType.Red))
        Await RefreshServiceStatusAsync()
    End Sub

    Private Async Sub BtnServiceUninstall_Click(sender As Object, e As EventArgs) Handles BtnServiceUninstall.Click
        If MyMsgBox("确定要卸载 OmniMix 后端服务吗？这不会删除后端程序文件，但服务模式将不可用。", "卸载后端服务", "卸载", "取消", IsWarn:=True) <> 1 Then Return

        LabServiceSummary.Text = "正在卸载后端服务..."
        Dim Success = Await OmniMixPlatformService.UninstallServiceAsync()
        Hint(If(Success, "后端服务已卸载。", "后端服务卸载失败。"), If(Success, HintType.Green, HintType.Red))
        Await RefreshServiceStatusAsync()
    End Sub

    Private Async Sub BtnServiceStart_Click(sender As Object, e As EventArgs) Handles BtnServiceStart.Click
        LabServiceSummary.Text = "正在启动后端服务..."
        Dim Success = Await OmniMixPlatformService.StartServiceAsync()
        Hint(If(Success, "后端服务启动命令已发送。", "后端服务启动失败。"), If(Success, HintType.Green, HintType.Red))
        Await RefreshServiceStatusAsync()
        If Success Then Await DiscoverBackendAfterServiceStartAsync()
    End Sub

    Private Async Sub BtnServiceStop_Click(sender As Object, e As EventArgs) Handles BtnServiceStop.Click
        If MyMsgBox("确定要停止 OmniMix 后端服务吗？停止后曲库、播放控制和模块 UI 会暂时不可用。", "停止后端服务", "停止", "取消", IsWarn:=True) <> 1 Then Return

        LabServiceSummary.Text = "正在停止后端服务..."
        Dim Success = Await OmniMixPlatformService.StopServiceAsync()
        If Success Then
            CurrentBaseUrl = ""
            FrmMain?.SetOmniMixConnectionStatus(False)
        End If
        Hint(If(Success, "后端服务已停止。", "后端服务停止失败。"), If(Success, HintType.Green, HintType.Red))
        Await RefreshServiceStatusAsync()
    End Sub

    Private Async Sub BtnServiceToggleAutoStart_Click(sender As Object, e As EventArgs) Handles BtnServiceToggleAutoStart.Click
        Dim State = Await OmniMixPlatformService.GetServiceStateAsync()
        If State = OmniMixServiceState.NotInstalled Then
            LabServiceSummary.Text = "服务尚未安装，无法切换自启动。"
            Return
        End If

        Dim CurrentAutoStart = Await OmniMixPlatformService.IsServiceAutoStartAsync()
        Dim Success = Await OmniMixPlatformService.SetServiceAutoStartAsync(Not CurrentAutoStart)
        Hint(If(Success, "服务自启动设置已更新。", "服务自启动设置失败。"), If(Success, HintType.Green, HintType.Red))
        Await RefreshServiceStatusAsync()
    End Sub

    Private Async Function DiscoverBackendAfterServiceStartAsync() As Task
        For i = 0 To 12
            Await Task.Delay(500)
            Dim Status = Await OmniMixApiClient.DiscoverAsync()
            If Status.IsOnline Then
                CurrentBaseUrl = Status.BaseUrl
                FrmMain?.SetOmniMixConnectionStatus(True, Status.BaseUrl)
                LabStatus.Text = InitialStatusText & vbCrLf & vbCrLf &
                    "后端状态：在线" & vbCrLf &
                    "连接地址：" & Status.BaseUrl & vbCrLf &
                    "启动方式：Windows 服务"
                If PageKey = "Settings" Then Await RefreshSettingsAsync(Status.BaseUrl)
                Return
            End If
        Next

        FrmMain?.SetOmniMixConnectionStatus(False)
        LabServiceSummary.Text = LabServiceSummary.Text & vbCrLf & "服务启动后暂未发现 /api/health，可稍后刷新。"
    End Function

    Private Shared Function GetServiceStateText(State As OmniMixServiceState) As String
        Select Case State
            Case OmniMixServiceState.Running
                Return "运行中"
            Case OmniMixServiceState.Installed
                Return "已安装，未运行"
            Case Else
                Return "未安装"
        End Select
    End Function

    Private Async Sub BtnArchivesRefresh_Click(sender As Object, e As EventArgs) Handles BtnArchivesRefresh.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshSettingsAsync(CurrentBaseUrl)
        End If
    End Sub

    Private Async Sub SettingsInstanceSelectButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Instance = TryCast(Button.Tag, OmniMixPlaybackInstanceInfo)
        If Instance Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Instance.Id) Then Return

        Try
            Await OmniMixApiClient.SetActiveInstanceAsync(CurrentBaseUrl, Instance.Id)
            ActiveInstanceId = Instance.Id
            LabInstanceStats.Text = "已设为当前实例：" & NonEmpty(Instance.GameName, Instance.Id)
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabInstanceStats.Text = "设置当前实例失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub SettingsInstanceMetaButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Instance = TryCast(Button.Tag, OmniMixPlaybackInstanceInfo)
        If Instance Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Instance.Id) Then Return

        Dim GameName = MyMsgBoxInput(
            "编辑实例元数据",
            "修改实例“" & Instance.Id & "”显示的游戏名称。",
            NonEmpty(Instance.GameName, Instance.Id),
            HintText:="游戏名称")
        If GameName Is Nothing Then Return

        Dim ModId = MyMsgBoxInput(
            "编辑实例元数据",
            "修改实例绑定的 Mod ID。",
            If(Instance.ModId, ""),
            HintText:="Mod ID")
        If ModId Is Nothing Then Return

        Dim Mode = MyMsgBoxInput(
            "编辑实例元数据",
            "修改实例模式。通常保持 ServerManaged 或 ClientManaged。",
            NonEmpty(Instance.Mode, "ServerManaged"),
            HintText:="ServerManaged / ClientManaged")
        If Mode Is Nothing Then Return

        Try
            Await OmniMixApiClient.SetInstanceMetaAsync(CurrentBaseUrl, Instance.Id, ModId.Trim(), GameName.Trim(), Mode.Trim())
            LabInstanceStats.Text = "实例元数据已保存：" & NonEmpty(GameName, Instance.Id)
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabInstanceStats.Text = "实例元数据保存失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub SettingsInstanceDeleteButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Instance = TryCast(Button.Tag, OmniMixPlaybackInstanceInfo)
        If Instance Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Instance.Id) Then Return
        If Instance.Attached Then
            LabInstanceStats.Text = "实例仍在线，暂不删除。"
            Return
        End If

        If MyMsgBox("确定要删除离线实例“" & NonEmpty(Instance.GameName, Instance.Id) & "”吗？此操作会删除该实例的后端记录。", "删除实例", "删除", "取消", IsWarn:=True) <> 1 Then Return

        Try
            Await OmniMixApiClient.DeleteInstanceAsync(CurrentBaseUrl, Instance.Id)
            If String.Equals(ActiveInstanceId, Instance.Id, StringComparison.OrdinalIgnoreCase) Then ActiveInstanceId = ""
            LabInstanceStats.Text = "实例已删除：" & Instance.Id
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabInstanceStats.Text = "删除实例失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub SettingsInstanceArchiveButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Instance = TryCast(Button.Tag, OmniMixPlaybackInstanceInfo)
        If Instance Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Instance.Id) Then Return

        Dim Label = MyMsgBoxInput(
            "保存实例归档",
            "将实例“" & Instance.Id & "”的当前播放列表、队列和均衡器设置保存为归档。",
            NonEmpty(Instance.GameName, Instance.Id),
            HintText:="归档名称")
        If Label Is Nothing Then Return

        Try
            Await OmniMixApiClient.ArchiveInstanceAsync(CurrentBaseUrl, Instance.Id, Label)
            LabArchiveSummary.Text = "已保存实例归档：" & NonEmpty(Label, Instance.Id)
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabArchiveSummary.Text = "保存实例归档失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub ArchiveRenameButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Archive = TryCast(Button.Tag, OmniMixArchiveInfo)
        If Archive Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Archive.InstanceId) Then Return

        Dim Label = MyMsgBoxInput(
            "重命名归档",
            "修改归档“" & Archive.DisplayName & "”的显示名称。",
            Archive.Label,
            HintText:="归档名称")
        If Label Is Nothing Then Return

        Try
            Await OmniMixApiClient.RenameArchiveAsync(CurrentBaseUrl, Archive.InstanceId, Label)
            LabArchiveSummary.Text = "归档已重命名。"
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabArchiveSummary.Text = "重命名归档失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub ArchiveInheritButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = TryCast(Button.Tag, Tuple(Of OmniMixArchiveInfo, String))
        If Payload Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Dim Archive = Payload.Item1
        If Archive Is Nothing OrElse String.IsNullOrWhiteSpace(Archive.InstanceId) Then Return

        Dim DefaultTargetId = Payload.Item2
        If String.IsNullOrWhiteSpace(DefaultTargetId) Then
            Try
                Dim Instances = Await OmniMixApiClient.GetInstancesAsync(CurrentBaseUrl)
                Dim Candidate = PickActiveInstance(Instances)
                If Candidate IsNot Nothing Then DefaultTargetId = Candidate.Id
            Catch
            End Try
        End If

        Dim TargetId = MyMsgBoxInput(
            "应用归档",
            "输入要继承归档“" & Archive.DisplayName & "”的目标实例 ID。归档未绑定在线实例时可能会被消费；已绑定时会复制设置。",
            If(DefaultTargetId, ""),
            HintText:="目标实例 ID")
        If TargetId Is Nothing Then Return
        TargetId = TargetId.Trim()
        If String.IsNullOrWhiteSpace(TargetId) Then
            LabArchiveSummary.Text = "目标实例 ID 不能为空。"
            Return
        End If

        If MyMsgBox("确定要把归档“" & Archive.DisplayName & "”应用到实例“" & TargetId & "”吗？", "应用归档", "应用", "取消") <> 1 Then Return

        Try
            Dim Result = Await OmniMixApiClient.InheritFromArchiveAsync(CurrentBaseUrl, TargetId, Archive.InstanceId)
            Dim Consumed = False
            Dim ConsumedElement As JsonElement
            If Result IsNot Nothing AndAlso Result.TryGetValue("consumed", ConsumedElement) AndAlso ConsumedElement.ValueKind = JsonValueKind.True Then
                Consumed = True
            End If
            LabArchiveSummary.Text = If(Consumed, "归档已消费并继承到实例：", "归档设置已复制到实例：") & TargetId
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabArchiveSummary.Text = "应用归档失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub ArchiveDeleteButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Archive = TryCast(Button.Tag, OmniMixArchiveInfo)
        If Archive Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(Archive.InstanceId) Then Return

        If MyMsgBox("确定要删除归档“" & Archive.DisplayName & "”吗？此操作不可撤销。", "删除归档", "删除", "取消", IsWarn:=True) <> 1 Then Return

        Try
            Await OmniMixApiClient.DeleteArchiveAsync(CurrentBaseUrl, Archive.InstanceId)
            LabArchiveSummary.Text = "归档已删除。"
            Await RefreshSettingsAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabArchiveSummary.Text = "删除归档失败：" & Ex.Message
        End Try
    End Sub

    Private Async Function RefreshEqualizerAsync(BaseUrl As String) As Task
        If PageKey <> "Settings" OrElse Not String.Equals(CurrentSettingsPane, "equalizer", StringComparison.OrdinalIgnoreCase) Then Return

        LabEqualizerSummary.Text = "正在读取均衡器..."
        PanEqualizerPresets.Children.Clear()
        PanEqualizerPoints.Children.Clear()
        SetEqualizerButtonsEnabled(False)

        Try
            Dim Instance = Await GetControllableInstanceAsync()
            If Instance Is Nothing OrElse String.IsNullOrWhiteSpace(Instance.Id) Then
                CurrentEqualizerInstanceId = ""
                CurrentEqualizerState = Nothing
                CurrentEqualizerPresets.Clear()
                LabEqualizerSummary.Text = "均衡器：暂无播放实例。等待游戏或音频端连接后可编辑。"
                PanEqualizerPoints.Children.Add(New MyListItem With {
                    .Title = "暂无可编辑实例",
                    .Info = "后端在线，但还没有播放实例可用于读取均衡器。",
                    .Height = 42,
                    .PaddingLeft = 8,
                    .Margin = New Thickness(0, 0, 0, 2),
                    .IsScaleAnimationEnabled = False,
                    .Type = MyListItem.CheckType.Clickable
                })
                Return
            End If

            CurrentEqualizerInstanceId = Instance.Id
            CurrentEqualizerState = NormalizeEqualizerState(Await OmniMixApiClient.GetInstanceEqualizerAsync(BaseUrl, Instance.Id))
            Try
                CurrentEqualizerPresets = Await OmniMixApiClient.GetInstanceEqualizerPresetsAsync(BaseUrl, Instance.Id)
            Catch
                CurrentEqualizerPresets = New Dictionary(Of String, OmniMixEqualizerStateInfo)
            End Try
            RenderEqualizer(Instance, CurrentEqualizerState, CurrentEqualizerPresets)
        Catch Ex As Exception
            CurrentEqualizerInstanceId = ""
            CurrentEqualizerState = Nothing
            CurrentEqualizerPresets.Clear()
            LabEqualizerSummary.Text = "均衡器读取失败：" & Ex.Message
            SetEqualizerButtonsEnabled(False)
        End Try
    End Function

    Private Sub RenderEqualizer(Instance As OmniMixPlaybackInstanceInfo, State As OmniMixEqualizerStateInfo, Presets As Dictionary(Of String, OmniMixEqualizerStateInfo))
        State = NormalizeEqualizerState(State)
        Presets = If(Presets, New Dictionary(Of String, OmniMixEqualizerStateInfo))

        SetEqualizerButtonsEnabled(True)
        BtnEqualizerToggle.Text = If(State.Enabled, "禁用均衡器", "启用均衡器")
        BtnEqualizerSoftClip.Text = If(State.SoftClipEnabled, "关闭软削波", "开启软削波")
        LabEqualizerSummary.Text = $"实例 {NonEmpty(Instance.Id, Instance.ClientId)} · {BuildEqualizerStateInfo(State)}"

        PanEqualizerPresets.Children.Clear()
        If Presets.Count = 0 Then
            PanEqualizerPresets.Children.Add(New MyListItem With {
                .Title = "暂无预设",
                .Info = "后端暂未返回均衡器预设。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
        Else
            For Each Preset In Presets.OrderBy(Function(Item) Item.Key)
                Dim Item As New MyListItem With {
                    .Title = Preset.Key,
                    .Info = BuildEqualizerStateInfo(Preset.Value),
                    .Height = 42,
                    .PaddingLeft = 8,
                    .Margin = New Thickness(0, 0, 0, 2),
                    .IsScaleAnimationEnabled = False,
                    .Type = MyListItem.CheckType.Clickable,
                    .Tag = Preset
                }
                Dim ApplyButton As New MyIconButton With {
                    .Logo = Logo.IconButtonOpen,
                    .LogoScale = 1.05,
                    .ToolTip = "应用预设",
                    .Tag = Tuple.Create(Preset.Key, Preset.Value)
                }
                AddHandler ApplyButton.Click, AddressOf EqualizerPresetApplyButton_Click
                Item.Buttons = New List(Of MyIconButton) From {ApplyButton}
                PanEqualizerPresets.Children.Add(Item)
            Next
        End If

        PanEqualizerPoints.Children.Clear()
        If State.Points.Count = 0 Then
            PanEqualizerPoints.Children.Add(New MyListItem With {
                .Title = "暂无控制点",
                .Info = "当前为平直响应，可添加控制点或应用预设。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        For Each Point In State.Points.OrderBy(Function(Item) Item.Frequency)
            Dim Item As New MyListItem With {
                .Title = GetEqualizerTypeLabel(Point.Type) & " · " & FormatFrequency(Point.Frequency),
                .Info = $"{FormatDb(Point.GainDb)} · Q {Point.Q.ToString("0.##", CultureInfo.InvariantCulture)} · {Point.Id}",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Point
            }

            Dim EditButton As New MyIconButton With {
                .Logo = Logo.IconButtonEdit,
                .LogoScale = 0.95,
                .ToolTip = "编辑控制点",
                .Tag = Point
            }
            AddHandler EditButton.Click, AddressOf EqualizerPointEditButton_Click

            Dim DeleteButton As New MyIconButton With {
                .Logo = Logo.IconButtonDelete,
                .LogoScale = 0.95,
                .ToolTip = "删除控制点",
                .Tag = Point
            }
            AddHandler DeleteButton.Click, AddressOf EqualizerPointDeleteButton_Click

            Item.Buttons = New List(Of MyIconButton) From {EditButton, DeleteButton}
            PanEqualizerPoints.Children.Add(Item)
        Next
    End Sub

    Private Sub SetEqualizerButtonsEnabled(Enabled As Boolean)
        BtnEqualizerRefresh.IsEnabled = Not String.IsNullOrWhiteSpace(CurrentBaseUrl)
        BtnEqualizerToggle.IsEnabled = Enabled
        BtnEqualizerSoftClip.IsEnabled = Enabled
        BtnEqualizerGlobalGain.IsEnabled = Enabled
        BtnEqualizerAddPoint.IsEnabled = Enabled
        BtnEqualizerReset.IsEnabled = Enabled
    End Sub

    Private Async Function SaveEqualizerStateAsync(State As OmniMixEqualizerStateInfo, Message As String) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(CurrentEqualizerInstanceId) Then Return

        Try
            State = NormalizeEqualizerState(State)
            Await OmniMixApiClient.PutInstanceEqualizerAsync(CurrentBaseUrl, CurrentEqualizerInstanceId, State)
            CurrentEqualizerState = CloneEqualizerState(State)
            Await RefreshEqualizerAsync(CurrentBaseUrl)
            If Not String.IsNullOrWhiteSpace(Message) Then LabEqualizerSummary.Text = Message & " " & LabEqualizerSummary.Text
        Catch Ex As Exception
            LabEqualizerSummary.Text = "均衡器保存失败：" & Ex.Message
        End Try
    End Function

    Private Async Sub BtnEqualizerRefresh_Click(sender As Object, e As EventArgs) Handles BtnEqualizerRefresh.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshEqualizerAsync(CurrentBaseUrl)
        End If
    End Sub

    Private Async Sub BtnEqualizerToggle_Click(sender As Object, e As EventArgs) Handles BtnEqualizerToggle.Click
        Dim State = CloneEqualizerState(CurrentEqualizerState)
        State.Enabled = Not State.Enabled
        Await SaveEqualizerStateAsync(State, If(State.Enabled, "已启用均衡器。", "已禁用均衡器。"))
    End Sub

    Private Async Sub BtnEqualizerSoftClip_Click(sender As Object, e As EventArgs) Handles BtnEqualizerSoftClip.Click
        Dim State = CloneEqualizerState(CurrentEqualizerState)
        State.SoftClipEnabled = Not State.SoftClipEnabled
        Await SaveEqualizerStateAsync(State, If(State.SoftClipEnabled, "已开启软削波。", "已关闭软削波。"))
    End Sub

    Private Async Sub BtnEqualizerGlobalGain_Click(sender As Object, e As EventArgs) Handles BtnEqualizerGlobalGain.Click
        Dim Gain As Double
        If Not PromptEqualizerNumber("全局增益", "输入均衡器全局增益（dB）。", If(CurrentEqualizerState Is Nothing, 0, CurrentEqualizerState.GlobalGainDb), -24, 24, Gain) Then Return

        Dim State = CloneEqualizerState(CurrentEqualizerState)
        State.GlobalGainDb = Gain
        Await SaveEqualizerStateAsync(State, "已更新全局增益。")
    End Sub

    Private Async Sub BtnEqualizerAddPoint_Click(sender As Object, e As EventArgs) Handles BtnEqualizerAddPoint.Click
        Dim TypeValue = PromptEqualizerType("Peaking")
        If String.IsNullOrWhiteSpace(TypeValue) Then Return

        Dim Frequency As Double
        If Not PromptEqualizerNumber("添加控制点", "输入频率（Hz）。", 1000, 20, 22000, Frequency) Then Return
        Dim Gain As Double
        If Not PromptEqualizerNumber("添加控制点", "输入增益（dB）。", 0, -24, 24, Gain) Then Return
        Dim Q As Double
        If Not PromptEqualizerNumber("添加控制点", "输入 Q 值。", 1, 0.1, 20, Q) Then Return

        Dim State = CloneEqualizerState(CurrentEqualizerState)
        State.Points.Add(New OmniMixEqualizerPointInfo With {
            .Id = "vb_" & Guid.NewGuid().ToString("N"),
            .Frequency = Frequency,
            .GainDb = Gain,
            .Q = Q,
            .Type = TypeValue
        })
        Await SaveEqualizerStateAsync(State, "已添加控制点。")
    End Sub

    Private Async Sub BtnEqualizerReset_Click(sender As Object, e As EventArgs) Handles BtnEqualizerReset.Click
        If MyMsgBox("确定要把当前实例的均衡器重置为平直响应吗？", "重置均衡器", "重置", "取消", IsWarn:=True) <> 1 Then Return

        Await SaveEqualizerStateAsync(New OmniMixEqualizerStateInfo With {
            .Enabled = True,
            .GlobalGainDb = 0,
            .SoftClipEnabled = True,
            .Points = New List(Of OmniMixEqualizerPointInfo)
        }, "已重置为平直响应。")
    End Sub

    Private Async Sub EqualizerPresetApplyButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = CType(Button.Tag, Tuple(Of String, OmniMixEqualizerStateInfo))
        Await SaveEqualizerStateAsync(CloneEqualizerState(Payload.Item2), "已应用预设“" & Payload.Item1 & "”。")
    End Sub

    Private Async Sub EqualizerPointEditButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Point = TryCast(Button.Tag, OmniMixEqualizerPointInfo)
        If Point Is Nothing Then Return

        Dim TypeValue = PromptEqualizerType(Point.Type)
        If String.IsNullOrWhiteSpace(TypeValue) Then Return
        Dim Frequency As Double
        If Not PromptEqualizerNumber("编辑控制点", "输入频率（Hz）。", Point.Frequency, 20, 22000, Frequency) Then Return
        Dim Gain As Double
        If Not PromptEqualizerNumber("编辑控制点", "输入增益（dB）。", Point.GainDb, -24, 24, Gain) Then Return
        Dim Q As Double
        If Not PromptEqualizerNumber("编辑控制点", "输入 Q 值。", Point.Q, 0.1, 20, Q) Then Return

        Dim State = CloneEqualizerState(CurrentEqualizerState)
        Dim Target = State.Points.FirstOrDefault(Function(Item) String.Equals(Item.Id, Point.Id, StringComparison.OrdinalIgnoreCase))
        If Target Is Nothing Then Return
        Target.Type = TypeValue
        Target.Frequency = Frequency
        Target.GainDb = Gain
        Target.Q = Q
        Await SaveEqualizerStateAsync(State, "已更新控制点。")
    End Sub

    Private Async Sub EqualizerPointDeleteButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Point = TryCast(Button.Tag, OmniMixEqualizerPointInfo)
        If Point Is Nothing Then Return
        If MyMsgBox("确定要删除控制点“" & FormatFrequency(Point.Frequency) & "”吗？", "删除控制点", "删除", "取消", IsWarn:=True) <> 1 Then Return

        Dim State = CloneEqualizerState(CurrentEqualizerState)
        State.Points.RemoveAll(Function(Item) String.Equals(Item.Id, Point.Id, StringComparison.OrdinalIgnoreCase))
        Await SaveEqualizerStateAsync(State, "已删除控制点。")
    End Sub

    Private Async Function RefreshPlaybackAsync(BaseUrl As String, Optional EnsureController As Boolean = True) As Task
        If PageKey <> "Home" Then Return

        If EnsureController Then LabPlaybackSummary.Text = "正在注册 GUI 控制端并读取播放实例..."

        Try
            If EnsureController Then Await OmniMixApiClient.ConnectControllerAsync(BaseUrl)
            Dim Instances = Await OmniMixApiClient.GetInstancesAsync(BaseUrl)
            Dim Config = Await OmniMixApiClient.GetConfigAsync(BaseUrl)
            Dim ActiveInstance = PickActiveInstance(Instances, ConfigString(Config, "active_instance", ""))
            ActiveInstanceId = If(ActiveInstance Is Nothing, "", ActiveInstance.Id)
            RenderPlayback(Instances, ActiveInstance)
            Await RefreshQueuePaneAsync(BaseUrl)
        Catch Ex As Exception
            ActiveInstanceId = ""
            CanControlActiveInstance = False
            LabPlaybackSummary.Text = "播放实例加载失败：" & Ex.Message
            RenderPlayback(New List(Of OmniMixPlaybackInstanceInfo), Nothing)
            RenderQueueItems(New List(Of OmniMixQueueItemInfo), IsShowingHistory)
        End Try
    End Function

    Private Sub RenderPlayback(Instances As List(Of OmniMixPlaybackInstanceInfo), ActiveInstance As OmniMixPlaybackInstanceInfo)
        Instances = If(Instances, New List(Of OmniMixPlaybackInstanceInfo))
        Dim AttachedCount = Instances.Where(Function(Instance) Instance.Attached).Count()
        Dim ServerCount = Instances.Where(Function(Instance) Instance.IsServerManaged).Count()
        LabPlaybackSummary.Text = $"已读取 {Instances.Count} 个播放实例；在线 {AttachedCount} 个，可由后端控制 {ServerCount} 个。"

        IsUpdatingPlaybackUi = True
        Try
            Dim CanControl = ActiveInstance IsNot Nothing AndAlso ActiveInstance.IsServerManaged
            CanControlActiveInstance = CanControl
            BtnPlaybackPrev.IsEnabled = CanControl
            BtnPlaybackToggle.IsEnabled = CanControl
            BtnPlaybackNext.IsEnabled = CanControl
            SliderVolume.IsEnabled = CanControl

            If ActiveInstance Is Nothing Then
                CanControlActiveInstance = False
                LabPlaybackTrack.Text = "没有曲目正在播放"
                LabPlaybackMeta.Text = "后端已连接，但还没有可控制的音频实例。"
                LabPlaybackInstance.Text = "GUI 控制端已注册；等待音频实例连接。"
                LabPlaybackPosition.Text = "进度：--"
                SliderVolume.Value = 100
                LabPlaybackVolume.Text = "100%"
                LabQueueSummary.Text = ""
                LabQueueSummary.Visibility = Visibility.Collapsed
                Return
            End If

            Dim Track = ActiveInstance.CurrentTrack
            If Track Is Nothing OrElse (String.IsNullOrWhiteSpace(Track.Uuid) AndAlso String.IsNullOrWhiteSpace(Track.Title)) Then
                LabPlaybackTrack.Text = If(ActiveInstance.IsPlaying, "正在播放", "暂无曲目")
                LabPlaybackMeta.Text = "当前实例还没有曲目信息。"
            Else
                LabPlaybackTrack.Text = NonEmpty(Track.Title, Track.Uuid)
                Dim TrackParts As New List(Of String)
                If Not String.IsNullOrWhiteSpace(Track.Artist) Then TrackParts.Add(Track.Artist)
                If Track.Duration > 0 Then TrackParts.Add(FormatDuration(Track.Duration))
                If Not String.IsNullOrWhiteSpace(Track.ModuleId) Then TrackParts.Add("来源 " & Track.ModuleId)
                LabPlaybackMeta.Text = String.Join(" · ", TrackParts)
            End If

            Dim InstanceParts As New List(Of String) From {
                "实例 " & NonEmpty(ActiveInstance.Id, ActiveInstance.ClientId),
                If(ActiveInstance.Attached, "在线", "离线"),
                If(ActiveInstance.IsServerManaged, "后端控制", "客户端控制"),
                If(ActiveInstance.IsPlaying, "播放中", "已暂停")
            }
            If ActiveInstance.QueueCount > 0 Then InstanceParts.Add($"队列 {ActiveInstance.QueueIndex + 1}/{ActiveInstance.QueueCount}")
            If ActiveInstance.HistoryCount > 0 Then InstanceParts.Add($"历史 {ActiveInstance.HistoryCount}")
            If ActiveInstance.TargetLatency > 0 Then InstanceParts.Add($"延迟 {CInt(Math.Round(ActiveInstance.TargetLatency * 1000))} ms")
            LabPlaybackInstance.Text = String.Join(" · ", InstanceParts)

            Dim Duration = If(Track Is Nothing, 0, Track.Duration)
            LabPlaybackPosition.Text = "进度：" & FormatDuration(ActiveInstance.Position) & If(Duration > 0, " / " & FormatDuration(Duration), "")
            Dim VolumeValue = CInt(Math.Max(0, Math.Min(100, Math.Round(ActiveInstance.Volume * 100))))
            SliderVolume.Value = VolumeValue
            LabPlaybackVolume.Text = VolumeValue & "%"
        Finally
            IsUpdatingPlaybackUi = False
        End Try
    End Sub

    Private Async Function RefreshQueuePaneAsync(BaseUrl As String) As Task
        If PageKey <> "Home" Then Return

        If String.IsNullOrWhiteSpace(ActiveInstanceId) Then
            RenderQueueItems(New List(Of OmniMixQueueItemInfo), IsShowingHistory)
            LabQueueSummary.Text = ""
            LabQueueSummary.Visibility = Visibility.Collapsed
            Return
        End If

        If IsShowingHistory Then
            Dim History = Await OmniMixApiClient.GetInstanceHistoryAsync(BaseUrl, ActiveInstanceId)
            RenderQueueItems(History, True)
        Else
            Dim Queue = Await OmniMixApiClient.GetInstanceQueueAsync(BaseUrl, ActiveInstanceId)
            RenderQueueItems(Queue, False)
        End If
    End Function

    Private Sub EnsureQueueToolbarIcons()
        BtnQueueClear.Logo = Logo.IconButtonDelete
        BtnQueueClear.LogoScale = 0.82
        BtnQueueClear.Theme = MyIconButton.Themes.Red
        BtnPlaybackRefresh.Logo = Logo.IconButtonRefresh
        BtnPlaybackRefresh.LogoScale = 0.84
        BtnPlaybackRefresh.Theme = MyIconButton.Themes.Color
    End Sub

    Private Sub UpdateQueueTabSelection(IsHistory As Boolean)
        If IsHistory Then
            PanQueueTab.SetResourceReference(Border.BackgroundProperty, "ColorBrushSemiTransparent")
            PanHistoryTab.SetResourceReference(Border.BackgroundProperty, "ColorBrush7")
        Else
            PanQueueTab.SetResourceReference(Border.BackgroundProperty, "ColorBrush7")
            PanHistoryTab.SetResourceReference(Border.BackgroundProperty, "ColorBrushSemiTransparent")
        End If
    End Sub

    Private Function BuildQueueRenderKey(Items As List(Of OmniMixQueueItemInfo), IsHistory As Boolean, CanEditQueue As Boolean, CanPlayQueueItems As Boolean) As String
        Dim Parts As New List(Of String) From {
            If(IsHistory, "history", "queue"),
            If(CanEditQueue, "edit", "read"),
            If(CanPlayQueueItems, "play", "nop"),
            Items.Count.ToString(CultureInfo.InvariantCulture)
        }

        For Each Item In Items.OrderBy(Function(QueueItem) QueueItem.Index)
            Parts.Add(String.Join("|", {
                Item.Index.ToString(CultureInfo.InvariantCulture),
                If(Item.Uuid, ""),
                If(Item.Title, ""),
                If(Item.Artist, ""),
                Item.Duration.ToString("0.###", CultureInfo.InvariantCulture),
                If(Item.ModuleId, ""),
                ResolveQueueItemCover(Item)
            }))
        Next

        Return String.Join(vbLf, Parts)
    End Function

    Private Sub RenderQueueItems(Items As List(Of OmniMixQueueItemInfo), IsHistory As Boolean)
        Items = If(Items, New List(Of OmniMixQueueItemInfo))
        EnsureQueueToolbarIcons()
        UpdateQueueTabSelection(IsHistory)
        BtnQueueTab.Text = "队列"
        BtnHistoryTab.Text = "历史"
        Dim CanEditQueue = Not String.IsNullOrWhiteSpace(CurrentBaseUrl) AndAlso Not String.IsNullOrWhiteSpace(ActiveInstanceId)
        BtnQueueClear.IsEnabled = CanEditQueue AndAlso Items.Count > 0
        BtnQueueClear.Opacity = If(BtnQueueClear.IsEnabled, 1, 0.35)

        Dim PaneName = If(IsHistory, "历史", "队列")
        LabQueueSummary.Visibility = Visibility.Visible
        LabQueueSummary.Text = If(Items.Count = 0, PaneName & "为空。", $"{PaneName}中有 {Items.Count} 首曲目。")

        Dim RenderKey = BuildQueueRenderKey(Items, IsHistory, CanEditQueue, CanControlActiveInstance)
        If String.Equals(CurrentQueueRenderKey, RenderKey, StringComparison.Ordinal) Then Return
        CurrentQueueRenderKey = RenderKey
        PanQueueList.Children.Clear()

        If Items.Count = 0 Then
            PanQueueList.Children.Add(New MyListItem With {
                .Title = If(IsHistory, "没有历史曲目", "没有待播放曲目"),
                .Info = If(IsHistory, "播放过的曲目会出现在这里。", "从音乐库选择歌曲后会出现在这里。"),
                .Height = 64,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        Dim OrderedItems = Items.OrderBy(Function(QueueItem) QueueItem.Index).ToList()
        For ListPosition = 0 To OrderedItems.Count - 1
            Dim CapturedItem = OrderedItems(ListPosition)
            Dim CapturedPosition = ListPosition
            Dim CapturedCount = OrderedItems.Count
            PanQueueList.Children.Add(New MyVirtualizingElement(Of MyListItem)(
                Function() CreateQueueListItem(CapturedItem, CapturedPosition, CapturedCount, IsHistory, PaneName, CanEditQueue, CanControlActiveInstance)
            ) With {.Height = 64})
        Next
    End Sub

    Private Function CreateQueueListItem(Item As OmniMixQueueItemInfo, ListPosition As Integer, TotalCount As Integer, IsHistory As Boolean, PaneName As String, CanEditQueue As Boolean, CanPlayQueueItems As Boolean) As MyListItem
        Dim InfoParts As New List(Of String)
        If Not String.IsNullOrWhiteSpace(Item.Artist) Then InfoParts.Add(Item.Artist)
        If Item.Duration > 0 Then InfoParts.Add(FormatDuration(Item.Duration))
        If Not String.IsNullOrWhiteSpace(Item.ModuleId) Then InfoParts.Add(Item.ModuleId)
        Dim ListItem As New MyListItem With {
            .Title = $"{Item.Index + 1}. {NonEmpty(Item.Title, Item.Uuid)}",
            .Info = String.Join(" · ", InfoParts),
            .Logo = ResolveQueueItemCover(Item),
            .LogoScale = 1,
            .LogoWidth = 58,
            .Height = 64,
            .PaddingLeft = 6,
            .Margin = New Thickness(0, 0, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = Item
        }

        Dim Buttons As New List(Of MyIconButton)
        Dim CanPlayQueueItem = CanEditQueue AndAlso CanPlayQueueItems AndAlso Not String.IsNullOrWhiteSpace(Item.Uuid)
        Dim PlayButton As New MyIconButton With {
            .Logo = IconPlay,
            .LogoScale = 0.78,
            .ToolTip = "播放",
            .Tag = Item.Uuid,
            .IsEnabled = CanPlayQueueItem,
            .Opacity = If(CanPlayQueueItem, 1, 0.35)
        }
        AddHandler PlayButton.Click, AddressOf QueueItemPlayButton_Click
        Buttons.Add(PlayButton)
        If CanEditQueue Then
            Dim MoveUpButton As New MyIconButton With {
                .Logo = IconMoveUp,
                .LogoScale = 0.82,
                .ToolTip = "上移",
                .Tag = Tuple.Create(Item.Index, IsHistory, -1),
                .IsEnabled = ListPosition > 0
            }
            AddHandler MoveUpButton.Click, AddressOf QueueItemMoveButton_Click
            Buttons.Add(MoveUpButton)

            Dim MoveDownButton As New MyIconButton With {
                .Logo = IconMoveDown,
                .LogoScale = 0.82,
                .ToolTip = "下移",
                .Tag = Tuple.Create(Item.Index, IsHistory, 1),
                .IsEnabled = ListPosition < TotalCount - 1
            }
            AddHandler MoveDownButton.Click, AddressOf QueueItemMoveButton_Click
            Buttons.Add(MoveDownButton)

            Dim RemoveButton As New MyIconButton With {
                .Logo = Logo.IconButtonDelete,
                .LogoScale = 1,
                .Theme = MyIconButton.Themes.Red,
                .ToolTip = "从" & PaneName & "中移除",
                .Tag = Tuple.Create(Item.Index, IsHistory)
            }
            AddHandler RemoveButton.Click, AddressOf QueueItemRemoveButton_Click
            Buttons.Add(RemoveButton)
        End If
        ListItem.Buttons = Buttons
        Return ListItem
    End Function

    Private Async Sub BtnQueueTab_Click(sender As Object, e As EventArgs) Handles BtnQueueTab.Click
        If Not IsShowingHistory Then Return
        IsShowingHistory = False
        Await RefreshQueuePaneOrBackendAsync()
    End Sub

    Private Async Sub BtnHistoryTab_Click(sender As Object, e As EventArgs) Handles BtnHistoryTab.Click
        If IsShowingHistory Then Return
        IsShowingHistory = True
        Await RefreshQueuePaneOrBackendAsync()
    End Sub

    Private Async Sub BtnQueueClear_Click(sender As Object, e As EventArgs) Handles BtnQueueClear.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) Then Return

        Try
            If IsShowingHistory Then
                Await OmniMixApiClient.ClearHistoryAsync(CurrentBaseUrl, ActiveInstanceId)
                LabQueueSummary.Text = "历史已清空。"
            Else
                Await OmniMixApiClient.ClearQueueAsync(CurrentBaseUrl, ActiveInstanceId)
                LabQueueSummary.Text = "队列已清空。"
            End If
            Await RefreshPlaybackAsync(CurrentBaseUrl, False)
        Catch Ex As Exception
            LabQueueSummary.Text = If(IsShowingHistory, "清空历史失败：", "清空队列失败：") & Ex.Message
        End Try
    End Sub

    Private Async Sub QueueItemPlayButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Uuid = TryCast(Button.Tag, String)
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) OrElse String.IsNullOrWhiteSpace(Uuid) Then Return

        Try
            Await OmniMixApiClient.PlayAsync(CurrentBaseUrl, ActiveInstanceId, Uuid)
            Await RefreshPlaybackAsync(CurrentBaseUrl, False)
        Catch Ex As Exception
            LabQueueSummary.Visibility = Visibility.Visible
            LabQueueSummary.Text = "播放失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub QueueItemRemoveButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = CType(Button.Tag, Tuple(Of Integer, Boolean))
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) Then Return

        Try
            If Payload.Item2 Then
                Await OmniMixApiClient.RemoveHistoryItemAsync(CurrentBaseUrl, ActiveInstanceId, Payload.Item1)
                LabQueueSummary.Text = "已从历史中移除曲目。"
            Else
                Await OmniMixApiClient.RemoveQueueItemAsync(CurrentBaseUrl, ActiveInstanceId, Payload.Item1)
                LabQueueSummary.Text = "已从队列中移除曲目。"
            End If
            Await RefreshPlaybackAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabQueueSummary.Text = If(Payload.Item2, "移除历史曲目失败：", "移除队列曲目失败：") & Ex.Message
        End Try
    End Sub

    Private Async Sub QueueItemMoveButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = CType(Button.Tag, Tuple(Of Integer, Boolean, Integer))
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) Then Return

        Dim FromIndex = Payload.Item1
        Dim ToIndex = Math.Max(0, FromIndex + Payload.Item3)
        Try
            If Payload.Item2 Then
                Await OmniMixApiClient.MoveHistoryItemAsync(CurrentBaseUrl, ActiveInstanceId, FromIndex, ToIndex)
                LabQueueSummary.Text = "已调整历史曲目顺序。"
            Else
                Await OmniMixApiClient.MoveQueueItemAsync(CurrentBaseUrl, ActiveInstanceId, FromIndex, ToIndex)
                LabQueueSummary.Text = "已调整队列曲目顺序。"
            End If
            Await RefreshPlaybackAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabQueueSummary.Text = If(Payload.Item2, "调整历史顺序失败：", "调整队列顺序失败：") & Ex.Message
        End Try
    End Sub

    Private Sub UpdatePlaybackAutoRefreshTimer()
        If PageKey = "Home" AndAlso IsLoaded AndAlso Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            If Not PlaybackAutoRefreshTimer.IsEnabled Then PlaybackAutoRefreshTimer.Start()
        Else
            If PlaybackAutoRefreshTimer.IsEnabled Then PlaybackAutoRefreshTimer.Stop()
        End If
    End Sub

    Private Async Sub PlaybackAutoRefreshTimer_Tick(sender As Object, e As EventArgs)
        If PageKey <> "Home" OrElse IsAutoRefreshingPlayback OrElse String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        IsAutoRefreshingPlayback = True
        Try
            Await RefreshPlaybackAsync(CurrentBaseUrl, False)
        Catch
            ' Keep timer refresh quiet.
        Finally
            IsAutoRefreshingPlayback = False
        End Try
    End Sub

    Private Async Function RefreshQueuePaneOrBackendAsync() As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshQueuePaneAsync(CurrentBaseUrl)
        End If
    End Function

    Private Shared Function PickActiveInstance(Instances As List(Of OmniMixPlaybackInstanceInfo), Optional PreferredId As String = "") As OmniMixPlaybackInstanceInfo
        If Instances Is Nothing OrElse Instances.Count = 0 Then Return Nothing
        If Not String.IsNullOrWhiteSpace(PreferredId) Then
            Dim Preferred = Instances.FirstOrDefault(Function(Instance) String.Equals(Instance.Id, PreferredId, StringComparison.OrdinalIgnoreCase))
            If Preferred IsNot Nothing Then Return Preferred
        End If
        Dim Current = Instances.FirstOrDefault(Function(Instance) Instance.Attached AndAlso Instance.IsServerManaged)
        If Current IsNot Nothing Then Return Current
        Current = Instances.FirstOrDefault(Function(Instance) Instance.IsServerManaged)
        If Current IsNot Nothing Then Return Current
        Current = Instances.FirstOrDefault(Function(Instance) Instance.Attached)
        If Current IsNot Nothing Then Return Current
        Return Instances.First()
    End Function

    Private Async Function SendPlaybackCommandAsync(Command As String) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) Then Return
        Try
            Await OmniMixApiClient.SendInstanceCommandAsync(CurrentBaseUrl, ActiveInstanceId, Command)
            Await RefreshPlaybackAsync(CurrentBaseUrl)
        Catch Ex As Exception
            LabPlaybackSummary.Text = "播放控制失败：" & Ex.Message
        End Try
    End Function

    Private Async Sub BtnPlaybackRefresh_Click(sender As Object, e As EventArgs) Handles BtnPlaybackRefresh.Click
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Await RefreshBackendStatusAsync()
        Else
            Await RefreshPlaybackAsync(CurrentBaseUrl)
        End If
    End Sub

    Private Async Sub BtnPlaybackPrev_Click(sender As Object, e As EventArgs) Handles BtnPlaybackPrev.Click
        Await SendPlaybackCommandAsync("prev")
    End Sub

    Private Async Sub BtnPlaybackToggle_Click(sender As Object, e As EventArgs) Handles BtnPlaybackToggle.Click
        Await SendPlaybackCommandAsync("toggle")
    End Sub

    Private Async Sub BtnPlaybackNext_Click(sender As Object, e As EventArgs) Handles BtnPlaybackNext.Click
        Await SendPlaybackCommandAsync("next")
    End Sub

    Private Async Sub SliderVolume_Change(sender As Object, user As Boolean) Handles SliderVolume.Change
        If IsUpdatingPlaybackUi Then Return
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(ActiveInstanceId) Then Return

        Dim Volume = SliderVolume.Value / 100.0
        LabPlaybackVolume.Text = SliderVolume.Value & "%"
        Try
            Await OmniMixApiClient.SetInstanceVolumeAsync(CurrentBaseUrl, ActiveInstanceId, Volume)
            LabPlaybackSummary.Text = "已更新音量为 " & SliderVolume.Value & "%。"
        Catch Ex As Exception
            LabPlaybackSummary.Text = "音量保存失败：" & Ex.Message
        End Try
    End Sub

    Private Async Function RefreshModulesAsync(BaseUrl As String) As Task
        If PageKey <> "Modules" Then Return

        UpdatePluginPaneVisibility()
        LabModulesSummary.Text = If(CurrentModulesPane = "game", "正在加载游戏集成状态...", If(CurrentModulesPane = "launchpad", "正在加载启动台...", "正在加载 Mod 列表..."))
        PanModulesList.Children.Clear()
        PanLaunchpadList.Children.Clear()
        PanGameIntegrationList.Children.Clear()

        Try
            If CurrentModulesPane = "game" Then
                Dim Stats = Await OmniMixApiClient.GetInstanceStatsAsync(BaseUrl)
                Dim Instances = Await OmniMixApiClient.GetInstancesAsync(BaseUrl)
                RenderGameIntegration(Stats, Instances)
            Else
                Dim Modules = Await OmniMixApiClient.GetModulesAsync(BaseUrl)
                CurrentModules = If(Modules, New List(Of OmniMixModuleInfo))
                If CurrentModulesPane = "launchpad" Then
                    RenderLaunchpad(CurrentModules)
                Else
                    RenderModules(CurrentModules)
                End If
            End If
        Catch Ex As Exception
            LabModulesSummary.Text = If(CurrentModulesPane = "game", "游戏集成状态加载失败：", If(CurrentModulesPane = "launchpad", "启动台加载失败：", "Mod 列表加载失败：")) & Ex.Message
        End Try
    End Function

    Private Sub UpdatePluginPaneVisibility()
        BtnModulesTab.Text = If(CurrentModulesPane = "launchpad", "启动台 ✓", If(CurrentModulesPane = "mod", "Mod ✓", "Mod"))
        BtnGameIntegrationTab.Text = If(CurrentModulesPane = "game", "游戏集成 ✓", "游戏集成")
        PanModulesList.Visibility = If(CurrentModulesPane = "mod", Visibility.Visible, Visibility.Collapsed)
        PanLaunchpadList.Visibility = If(CurrentModulesPane = "launchpad", Visibility.Visible, Visibility.Collapsed)
        PanGameIntegrationList.Visibility = If(CurrentModulesPane = "game", Visibility.Visible, Visibility.Collapsed)
        CardModules.Title = If(CurrentModulesPane = "game", "插件 - 游戏集成", If(CurrentModulesPane = "launchpad", "插件 - 启动台", "插件 - Mod"))
    End Sub

    Private Sub BtnModulesTab_Click(sender As Object, e As EventArgs) Handles BtnModulesTab.Click
        SetModulesPane(If(CurrentModulesPane = "launchpad", "mod", "launchpad"))
    End Sub

    Private Sub BtnGameIntegrationTab_Click(sender As Object, e As EventArgs) Handles BtnGameIntegrationTab.Click
        SetModulesPane("game")
    End Sub

    Private Sub RenderGameIntegration(Stats As OmniMixInstanceStatsInfo, Instances As List(Of OmniMixPlaybackInstanceInfo))
        Stats = If(Stats, New OmniMixInstanceStatsInfo)
        Instances = If(Instances, New List(Of OmniMixPlaybackInstanceInfo))
        PanGameIntegrationList.Children.Clear()

        Dim AttachedCount = Instances.Where(Function(Instance) Instance.Attached).Count()
        Dim Games = OmniMixModDeploymentService.GetGameCatalog()
        LabModulesSummary.Text = $"游戏集成：{Games.Count} 个支持游戏；后端 {Stats.InstanceCount} 个实例，{AttachedCount} 个在线音频端，{Stats.ControllerClients} 个控制端。"

        For Each Game In Games
            AddGameIntegrationGameItem(Game)
        Next

        If DeploymentLogs.Count > 0 Then
            PanGameIntegrationList.Children.Add(New MyListItem With {
                .Title = "最近部署日志",
                .Info = String.Join("  /  ", DeploymentLogs.TakeLast(Math.Min(DeploymentLogs.Count, 4))),
                .Height = 54,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 8, 0, 8),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
        End If

        If Instances.Count = 0 Then
            PanGameIntegrationList.Children.Add(New MyListItem With {
                .Title = "暂无游戏集成实例",
                .Info = "后端在线，但还没有游戏或音频端连接到 OmniMix。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        PanGameIntegrationList.Children.Add(New MyListItem With {
            .Title = "后端实例状态",
            .Info = "在线游戏或已登记的离线播放实例。",
            .Height = 38,
            .PaddingLeft = 8,
            .Margin = New Thickness(0, 8, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable
        })

        For Each Instance In Instances.OrderByDescending(Function(Item) Item.Attached).ThenBy(Function(Item) Item.Id)
            Dim InfoParts As New List(Of String)
            InfoParts.Add(If(Instance.Attached, "在线", "离线"))
            InfoParts.Add(If(Instance.IsServerManaged, "后端控制", "客户端控制"))
            If Not String.IsNullOrWhiteSpace(Instance.GameName) Then InfoParts.Add(Instance.GameName)
            If Not String.IsNullOrWhiteSpace(Instance.ModId) Then InfoParts.Add("Mod " & Instance.ModId)
            If Instance.Attached AndAlso Not Instance.SharedMemoryReady Then InfoParts.Add("共享内存未就绪")
            InfoParts.Add("队列 " & Instance.QueueCount)

            PanGameIntegrationList.Children.Add(New MyListItem With {
                .Title = NonEmpty(Instance.Id, Instance.ClientId),
                .Info = String.Join(" · ", InfoParts),
                .Height = 44,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Instance
            })
        Next
    End Sub

    Private Sub AddGameIntegrationGameItem(Game As OmniMixGameDeclaration)
        Dim GamePath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
        Dim IsValidPath = OmniMixModDeploymentService.VerifyGameDirectory(GamePath, Game)
        Dim ModInfo = OmniMixModDeploymentService.GetPrimaryMod(Game)
        Dim BepInExStatus = If(Game.SupportedFrameworks.Contains("bepinex_5"), OmniMixModDeploymentService.CheckBepInExStatus(GamePath), OmniMixBepInExStatus.NotInstalled)
        Dim ModStatus = OmniMixModDeploymentService.CheckModStatus(GamePath, ModInfo)
        Dim InfoParts As New List(Of String)

        InfoParts.Add(If(IsValidPath, "目录有效", If(String.IsNullOrWhiteSpace(GamePath), "未选择目录", "目录无效")))
        If Not String.IsNullOrWhiteSpace(GamePath) Then InfoParts.Add(GamePath)
        If Game.SupportedFrameworks.Contains("bepinex_5") Then InfoParts.Add("BepInEx " & GetBepInExStatusText(BepInExStatus))
        If ModInfo IsNot Nothing Then
            InfoParts.Add(ModInfo.Name & " " & If(ModStatus = OmniMixModInstallStatus.Installed, "已安装", "未安装"))
            If Not OmniMixModDeploymentService.IsPackageAvailable(ModInfo.ArchiveName) Then InfoParts.Add("缺少 " & ModInfo.ArchiveName)
        End If

        Dim Buttons As New List(Of MyIconButton)
        Dim SelectButton As New MyIconButton With {
            .Logo = Logo.IconButtonOpen,
            .LogoScale = 1.05,
            .ToolTip = "选择游戏目录",
            .Tag = Game.Id
        }
        AddHandler SelectButton.Click, AddressOf GamePathSelectButton_Click
        Buttons.Add(SelectButton)

        If Not String.IsNullOrWhiteSpace(GamePath) AndAlso Directory.Exists(GamePath) Then
            Dim OpenButton As New MyIconButton With {
                .Logo = Logo.IconButtonList,
                .LogoScale = 1.0,
                .ToolTip = "打开游戏目录",
                .Tag = GamePath
            }
            AddHandler OpenButton.Click, AddressOf GamePathOpenButton_Click
            Buttons.Add(OpenButton)
        End If

        If Game.SupportedFrameworks.Contains("bepinex_5") Then
            If BepInExStatus = OmniMixBepInExStatus.Managed Then
                Dim UninstallButton As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .LogoScale = 1.0,
                    .ToolTip = "卸载 OmniMix 管理的 BepInEx",
                    .Tag = Game.Id
                }
                AddHandler UninstallButton.Click, AddressOf BepInExUninstallButton_Click
                Buttons.Add(UninstallButton)
            Else
                Dim InstallButton As New MyIconButton With {
                    .Logo = Logo.IconButtonSave,
                    .LogoScale = 1.0,
                    .ToolTip = If(OmniMixModDeploymentService.IsPackageAvailable("BepInEx_win_x64_5.4.23.5.zip"), "安装 BepInEx", "缺少 BepInEx 压缩包"),
                    .Tag = Game.Id,
                    .IsEnabled = IsValidPath AndAlso OmniMixModDeploymentService.IsPackageAvailable("BepInEx_win_x64_5.4.23.5.zip")
                }
                AddHandler InstallButton.Click, AddressOf BepInExInstallButton_Click
                Buttons.Add(InstallButton)
            End If
        End If

        If ModInfo IsNot Nothing Then
            If ModStatus = OmniMixModInstallStatus.Installed Then
                Dim UninstallModButton As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .LogoScale = 1.0,
                    .ToolTip = "卸载 " & ModInfo.Name,
                    .Tag = Game.Id
                }
                AddHandler UninstallModButton.Click, AddressOf ModUninstallButton_Click
                Buttons.Add(UninstallModButton)
            Else
                Dim InstallModButton As New MyIconButton With {
                    .Logo = Logo.IconButtonSetup,
                    .LogoScale = 1.0,
                    .ToolTip = If(OmniMixModDeploymentService.IsPackageAvailable(ModInfo.ArchiveName), "安装 " & ModInfo.Name, "缺少 " & ModInfo.ArchiveName),
                    .Tag = Game.Id,
                    .IsEnabled = IsValidPath AndAlso OmniMixModDeploymentService.IsPackageAvailable(ModInfo.ArchiveName)
                }
                AddHandler InstallModButton.Click, AddressOf ModInstallButton_Click
                Buttons.Add(InstallModButton)
            End If
        End If

        Dim Item As New MyListItem With {
            .Title = Game.Name,
            .Info = String.Join(" · ", InfoParts),
            .Height = 54,
            .PaddingLeft = 8,
            .Margin = New Thickness(0, 0, 0, 4),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = Game
        }
        Item.Buttons = Buttons
        PanGameIntegrationList.Children.Add(Item)
    End Sub

    Private Async Sub GamePathSelectButton_Click(sender As Object, e As EventArgs)
        Dim Game = OmniMixModDeploymentService.GetGame(TryCast(CType(sender, FrameworkElement).Tag, String))
        If Game Is Nothing Then Return

        Using Dialog As New System.Windows.Forms.FolderBrowserDialog()
            Dialog.Description = "选择 " & Game.Name & " 的游戏根目录"
            Dialog.ShowNewFolderButton = False
            Dim OldPath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
            If Directory.Exists(OldPath) Then Dialog.SelectedPath = OldPath
            If Dialog.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
            OmniMixModDeploymentService.SaveGamePath(Game.Id, Dialog.SelectedPath)
            If OmniMixModDeploymentService.VerifyGameDirectory(Dialog.SelectedPath, Game) Then
                If Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
                    Await OmniMixApiClient.AddPortFileDirAsync(CurrentBaseUrl, Dialog.SelectedPath)
                End If
                Hint("已保存游戏目录：" & Game.Name, HintType.Green)
            Else
                Hint("目录已保存，但未通过游戏签名校验。", HintType.Red)
            End If
        End Using

        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            RenderGameIntegration(New OmniMixInstanceStatsInfo, New List(Of OmniMixPlaybackInstanceInfo))
        Else
            Await RefreshModulesAsync(CurrentBaseUrl)
        End If
    End Sub

    Private Sub GamePathOpenButton_Click(sender As Object, e As EventArgs)
        Dim GamePath = TryCast(CType(sender, FrameworkElement).Tag, String)
        If Not String.IsNullOrWhiteSpace(GamePath) Then OpenExplorer(GamePath)
    End Sub

    Private Async Sub BepInExInstallButton_Click(sender As Object, e As EventArgs)
        Dim Game = OmniMixModDeploymentService.GetGame(TryCast(CType(sender, FrameworkElement).Tag, String))
        If Game Is Nothing Then Return
        Dim GamePath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
        If Not OmniMixModDeploymentService.VerifyGameDirectory(GamePath, Game) Then
            LabModulesSummary.Text = "游戏目录无效，请先选择正确的游戏根目录。"
            Return
        End If
        If OmniMixModDeploymentService.CheckBepInExStatus(GamePath) = OmniMixBepInExStatus.Unmanaged Then
            If MyMsgBox("检测到该目录已有非 OmniMix 管理的 BepInEx。继续安装可能会覆盖现有加载器文件。", "安装 BepInEx", "继续", "取消", IsWarn:=True) <> 1 Then Return
        End If

        LabModulesSummary.Text = "正在安装 BepInEx..."
        Dim Logs As New List(Of String)
        Dim Success = Await Task.Run(Function() OmniMixModDeploymentService.DeployBepInEx(GamePath, Logs))
        DeploymentLogs = Logs
        Hint(If(Success, "BepInEx 安装完成。", "BepInEx 安装失败。"), If(Success, HintType.Green, HintType.Red))
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then RenderGameIntegration(New OmniMixInstanceStatsInfo, New List(Of OmniMixPlaybackInstanceInfo)) Else Await RefreshModulesAsync(CurrentBaseUrl)
    End Sub

    Private Async Sub BepInExUninstallButton_Click(sender As Object, e As EventArgs)
        Dim Game = OmniMixModDeploymentService.GetGame(TryCast(CType(sender, FrameworkElement).Tag, String))
        If Game Is Nothing Then Return
        Dim GamePath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
        If MyMsgBox("确定要卸载 OmniMix 管理的 BepInEx 吗？非 OmniMix 管理的文件不会被保留在这次删除范围内。", "卸载 BepInEx", "卸载", "取消", IsWarn:=True) <> 1 Then Return

        LabModulesSummary.Text = "正在卸载 BepInEx..."
        Dim Logs As New List(Of String)
        Dim Success = Await Task.Run(Function() OmniMixModDeploymentService.UndeployBepInEx(GamePath, Logs))
        DeploymentLogs = Logs
        Hint(If(Success, "BepInEx 卸载完成。", "BepInEx 卸载失败。"), If(Success, HintType.Green, HintType.Red))
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then RenderGameIntegration(New OmniMixInstanceStatsInfo, New List(Of OmniMixPlaybackInstanceInfo)) Else Await RefreshModulesAsync(CurrentBaseUrl)
    End Sub

    Private Async Sub ModInstallButton_Click(sender As Object, e As EventArgs)
        Dim Game = OmniMixModDeploymentService.GetGame(TryCast(CType(sender, FrameworkElement).Tag, String))
        If Game Is Nothing Then Return
        Dim ModInfo = OmniMixModDeploymentService.GetPrimaryMod(Game)
        If ModInfo Is Nothing Then Return
        Dim GamePath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
        If Not OmniMixModDeploymentService.VerifyGameDirectory(GamePath, Game) Then
            LabModulesSummary.Text = "游戏目录无效，请先选择正确的游戏根目录。"
            Return
        End If
        If ModInfo.UsesFramework AndAlso OmniMixModDeploymentService.CheckBepInExStatus(GamePath) = OmniMixBepInExStatus.NotInstalled Then
            LabModulesSummary.Text = "请先安装 BepInEx，再安装 " & ModInfo.Name & "。"
            Return
        End If
        If Not OmniMixModDeploymentService.IsPackageAvailable(ModInfo.ArchiveName) Then
            LabModulesSummary.Text = "缺少 Mod 包：" & ModInfo.ArchiveName
            Return
        End If

        LabModulesSummary.Text = "正在安装 " & ModInfo.Name & "..."
        Dim Logs As New List(Of String)
        Dim BackendPort = GetPortFromBaseUrl(CurrentBaseUrl, 17890)
        Dim InstanceId = Await Task.Run(Function() OmniMixModDeploymentService.DeployMod(GamePath, ModInfo, BackendPort, Logs))
        DeploymentLogs = Logs
        If String.IsNullOrWhiteSpace(InstanceId) Then
            Hint(ModInfo.Name & " 安装失败。", HintType.Red)
        Else
            Await RegisterDeployedInstanceAsync(InstanceId, ModInfo, GamePath)
            Hint(ModInfo.Name & " 安装完成。", HintType.Green)
        End If
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then RenderGameIntegration(New OmniMixInstanceStatsInfo, New List(Of OmniMixPlaybackInstanceInfo)) Else Await RefreshModulesAsync(CurrentBaseUrl)
    End Sub

    Private Async Sub ModUninstallButton_Click(sender As Object, e As EventArgs)
        Dim Game = OmniMixModDeploymentService.GetGame(TryCast(CType(sender, FrameworkElement).Tag, String))
        If Game Is Nothing Then Return
        Dim ModInfo = OmniMixModDeploymentService.GetPrimaryMod(Game)
        If ModInfo Is Nothing Then Return
        Dim GamePath = OmniMixModDeploymentService.LoadGamePath(Game.Id)
        If MyMsgBox("确定要卸载 " & ModInfo.Name & " 吗？", "卸载 Mod", "卸载", "取消", IsWarn:=True) <> 1 Then Return

        LabModulesSummary.Text = "正在卸载 " & ModInfo.Name & "..."
        Dim Logs As New List(Of String)
        Dim Success = Await Task.Run(Function() OmniMixModDeploymentService.UndeployMod(GamePath, ModInfo, Logs))
        DeploymentLogs = Logs
        Hint(If(Success, ModInfo.Name & " 卸载完成。", ModInfo.Name & " 卸载失败。"), If(Success, HintType.Green, HintType.Red))
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then RenderGameIntegration(New OmniMixInstanceStatsInfo, New List(Of OmniMixPlaybackInstanceInfo)) Else Await RefreshModulesAsync(CurrentBaseUrl)
    End Sub

    Private Async Function RegisterDeployedInstanceAsync(InstanceId As String, ModInfo As OmniMixModDeclaration, GamePath As String) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(InstanceId) OrElse ModInfo Is Nothing Then Return
        Try
            If Not String.IsNullOrWhiteSpace(GamePath) Then
                Await OmniMixApiClient.AddPortFileDirAsync(CurrentBaseUrl, GamePath)
            End If
            Await OmniMixApiClient.SetInstanceMetaAsync(CurrentBaseUrl, InstanceId, ModInfo.Id, ModInfo.Name, ModInfo.Mode)
            Await OmniMixApiClient.UpdateInstanceProfileAsync(CurrentBaseUrl, InstanceId, BuildDefaultInstanceProfile())
        Catch Ex As Exception
            DeploymentLogs.Add("[" & Date.Now.ToString("HH:mm:ss") & "] Warning: failed to register backend profile: " & Ex.Message)
        End Try
    End Function

    Private Function BuildDefaultInstanceProfile() As Dictionary(Of String, Object)
        Dim Queue As New Dictionary(Of String, Object) From {
            {"Id", "default"},
            {"Name", "Default"},
            {"PlaylistSources", New List(Of Object)},
            {"SongUuids", New List(Of String)},
            {"HistoryUuids", New List(Of String)},
            {"Index", -1},
            {"HistoryPosition", -1},
            {"PlaylistPosition", 0},
            {"Shuffle", False},
            {"RepeatMode", "none"}
        }
        Return New Dictionary(Of String, Object) From {
            {"ActiveQueueId", "default"},
            {"Volume", 1.0},
            {"Queues", New List(Of Object) From {Queue}}
        }
    End Function

    Private Shared Function GetPortFromBaseUrl(BaseUrl As String, Fallback As Integer) As Integer
        Try
            Dim Parsed As New Uri(BaseUrl)
            If Parsed.Port > 0 Then Return Parsed.Port
        Catch
        End Try
        Return Fallback
    End Function

    Private Shared Function GetBepInExStatusText(Status As OmniMixBepInExStatus) As String
        Select Case Status
            Case OmniMixBepInExStatus.Managed
                Return "已由 OmniMix 管理"
            Case OmniMixBepInExStatus.Unmanaged
                Return "已安装（非 OmniMix 管理）"
            Case Else
                Return "未安装"
        End Select
    End Function

    Private Sub RenderModules(Modules As List(Of OmniMixModuleInfo))
        Modules = If(Modules, New List(Of OmniMixModuleInfo))
        CurrentModules = Modules
        PanModulesList.Children.Clear()

        Dim LoadedCount = Modules.Where(Function(ModuleInfo) Not String.IsNullOrWhiteSpace(ModuleInfo.LoadedAt)).Count()
        Dim EnabledCount = Modules.Where(Function(ModuleInfo) ModuleInfo.Enabled).Count()
        LabModulesSummary.Text = $"已读取 {Modules.Count} 个模块；当前已加载 {LoadedCount} 个，配置为启用 {EnabledCount} 个。"

        If Modules.Count = 0 Then
            PanModulesList.Children.Add(New MyListItem With {
                .Title = "暂无模块",
                .Info = "后端已连接，但当前没有已加载的音乐源模块。",
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        For Each ModuleInfo In Modules.OrderBy(Function(Item) Item.Priority).ThenBy(Function(Item) Item.Name)
            AddModuleItem(ModuleInfo)
        Next
    End Sub

    Private Sub RenderLaunchpad(Modules As List(Of OmniMixModuleInfo))
        Modules = If(Modules, New List(Of OmniMixModuleInfo))
        CurrentModules = Modules
        PanLaunchpadList.Children.Clear()

        Dim Links As New List(Of Tuple(Of OmniMixModuleInfo, OmniMixModuleLinkEntryInfo))
        For Each ModuleInfo In Modules.OrderBy(Function(Item) Item.Priority).ThenBy(Function(Item) Item.Name)
            For Each LinkEntry In If(ModuleInfo.LinkEntries, New List(Of OmniMixModuleLinkEntryInfo))
                Links.Add(Tuple.Create(ModuleInfo, LinkEntry))
            Next
        Next

        LabModulesSummary.Text = $"启动台：{Links.Count} 个模块快捷入口。"
        If Links.Count = 0 Then
            PanLaunchpadList.Children.Add(New MyListItem With {
                .Title = "暂无快捷入口",
                .Info = "已加载的模块暂未提供启动台入口。",
                .Width = 320,
                .Height = 48,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            Return
        End If

        For Each Payload In Links
            AddLaunchpadItem(Payload.Item1, Payload.Item2)
        Next
    End Sub

    Private Sub AddLaunchpadItem(ModuleInfo As OmniMixModuleInfo, LinkEntry As OmniMixModuleLinkEntryInfo)
        Dim Item As New MyListItem With {
            .Title = NonEmpty(LinkEntry.Title, LinkEntry.Id),
            .Info = NonEmpty(ModuleInfo.Name, ModuleInfo.Id),
            .Width = 150,
            .Height = 74,
            .PaddingLeft = 8,
            .Logo = ModuleLinkIcon(LinkEntry),
            .LogoScale = 0.9,
            .Margin = New Thickness(0, 0, 8, 8),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = Tuple.Create(ModuleInfo, LinkEntry)
        }
        AddHandler Item.Click, AddressOf LaunchpadItem_Click
        PanLaunchpadList.Children.Add(Item)
    End Sub

    Private Async Sub LaunchpadItem_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Payload = TryCast(CType(sender, FrameworkElement).Tag, Tuple(Of OmniMixModuleInfo, OmniMixModuleLinkEntryInfo))
        If Payload Is Nothing Then Return
        Await OpenModuleUiAsync(Payload.Item1, "link", Payload.Item2.Id, Payload.Item2.Title)
    End Sub

    Private Sub AddModuleItem(ModuleInfo As OmniMixModuleInfo)
        Dim Item As New MyListItem With {
            .Title = NonEmpty(ModuleInfo.Name, ModuleInfo.Id),
            .Info = BuildModuleInfoText(ModuleInfo),
            .Height = 46,
            .PaddingLeft = 8,
            .Margin = New Thickness(0, 0, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = ModuleInfo
        }

        Dim Buttons As New List(Of MyIconButton)
        Dim EnableButton As New MyIconButton With {
            .Logo = If(ModuleInfo.Enabled, Logo.IconButtonCheck, Logo.IconButtonStop),
            .LogoScale = 0.9,
            .ToolTip = If(ModuleInfo.Enabled, "禁用模块", "启用模块"),
            .Theme = If(ModuleInfo.Enabled, MyIconButton.Themes.Color, MyIconButton.Themes.Red),
            .Tag = ModuleInfo
        }
        AddHandler EnableButton.Click, AddressOf ModuleEnableButton_Click
        Buttons.Add(EnableButton)

        For Each LinkEntry In If(ModuleInfo.LinkEntries, New List(Of OmniMixModuleLinkEntryInfo))
            Dim LinkButton As New MyIconButton With {
                .Logo = Logo.IconButtonOpen,
                .LogoScale = 1.05,
                .ToolTip = "打开快捷入口：" & NonEmpty(LinkEntry.Title, LinkEntry.Id),
                .Tag = Tuple.Create(ModuleInfo, LinkEntry)
            }
            AddHandler LinkButton.Click, AddressOf ModuleLinkButton_Click
            Buttons.Add(LinkButton)
        Next
        If ModuleInfo.HasSettingsUI Then
            Dim SettingsButton As New MyIconButton With {
                .Logo = Logo.IconButtonSetup,
                .LogoScale = 1.05,
                .ToolTip = "打开设置 UI",
                .Tag = ModuleInfo
            }
            AddHandler SettingsButton.Click, AddressOf ModuleSettingsButton_Click
            Buttons.Add(SettingsButton)
        End If
        Item.Buttons = Buttons

        AddHandler Item.Click, AddressOf ModuleItem_Click
        PanModulesList.Children.Add(Item)
    End Sub

    Private Async Sub ModuleItem_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Item = CType(sender, MyListItem)
        Dim ModuleInfo = CType(Item.Tag, OmniMixModuleInfo)
        If ModuleInfo.HasSettingsUI Then
            Await OpenModuleUiAsync(ModuleInfo, "settings")
        ElseIf ModuleInfo.LinkEntries IsNot Nothing AndAlso ModuleInfo.LinkEntries.Count > 0 Then
            Dim LinkEntry = ModuleInfo.LinkEntries(0)
            Await OpenModuleUiAsync(ModuleInfo, "link", LinkEntry.Id, LinkEntry.Title)
        Else
            Await OpenModuleUiAsync(ModuleInfo, "default")
        End If
    End Sub

    Private Async Sub ModuleSettingsButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim ModuleInfo = CType(Button.Tag, OmniMixModuleInfo)
        Await OpenModuleUiAsync(ModuleInfo, "settings")
    End Sub

    Private Async Sub ModuleLinkButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = CType(Button.Tag, Tuple(Of OmniMixModuleInfo, OmniMixModuleLinkEntryInfo))
        Await OpenModuleUiAsync(Payload.Item1, "link", Payload.Item2.Id, Payload.Item2.Title)
    End Sub

    Private Async Function OpenModuleUiAsync(ModuleInfo As OmniMixModuleInfo, UiKind As String, Optional LinkId As String = "", Optional LinkTitle As String = "") As Task
        If ModuleInfo Is Nothing OrElse String.IsNullOrWhiteSpace(ModuleInfo.Id) Then Return
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        PreviousModulesPaneForDetail = CurrentModulesPane
        ModuleUiRequestSerial += 1
        Dim RequestSerial = ModuleUiRequestSerial
        CurrentModuleId = ModuleInfo.Id
        CurrentUiKind = UiKind
        CurrentLinkId = If(LinkId, "")
        ExpandedModuleTitle = NonEmpty(LinkTitle, NonEmpty(ModuleInfo.Name, ModuleInfo.Id))
        Dim UiLabel = If(UiKind = "settings", "模块设置", If(UiKind = "link", "快捷入口", "模块 UI"))
        LabModuleUiTitle.Text = UiLabel & " - " & ExpandedModuleTitle
        CardModules.Visibility = Visibility.Collapsed
        CardModuleUi.Visibility = Visibility.Visible
        PanModuleUi.Children.Clear()
        LabModuleUiSummary.Text = "正在加载 " & NonEmpty(ModuleInfo.Name, ModuleInfo.Id) & " 的" & UiLabel & "..."

        Try
            Await WsClient.ConnectAsync(CurrentBaseUrl)

            Dim Tree As OmniMixRawNodeData
            If UiKind = "settings" Then
                Tree = Await OmniMixApiClient.GetModuleSettingsUiAsync(CurrentBaseUrl, ModuleInfo.Id)
            ElseIf UiKind = "link" Then
                Tree = Await OmniMixApiClient.GetModuleLinkUiAsync(CurrentBaseUrl, ModuleInfo.Id, CurrentLinkId)
            Else
                Tree = Await OmniMixApiClient.GetModuleUiAsync(CurrentBaseUrl, ModuleInfo.Id)
            End If
            If RequestSerial <> ModuleUiRequestSerial Then Return
            PanModuleUi.Children.Clear()
            If Tree IsNot Nothing Then
                PanModuleUi.Children.Add(OmniMixRawNodeRenderer.Render(Tree, CurrentBaseUrl, AddressOf ModuleUiEvent_Dispatch))
            Else
                PanModuleUi.Children.Add(New MyListItem With {
                    .Title = "暂无可显示内容",
                    .Info = "模块没有返回可渲染的 UI。",
                    .Height = 42,
                    .PaddingLeft = 8,
                    .Margin = New Thickness(0, 0, 0, 2),
                    .IsScaleAnimationEnabled = False,
                    .Type = MyListItem.CheckType.Clickable
                })
            End If
            LabModulesSummary.Text = "已加载模块 UI：" & ExpandedModuleTitle
            LabModuleUiSummary.Text = "已加载模块 UI，交互事件将通过 WebSocket 分发到后端。"
        Catch Ex As Exception
            If RequestSerial <> ModuleUiRequestSerial Then Return
            PanModuleUi.Children.Clear()
            PanModuleUi.Children.Add(New MyListItem With {
                .Title = "加载失败",
                .Info = Ex.Message,
                .Height = 42,
                .PaddingLeft = 8,
                .Margin = New Thickness(0, 0, 0, 2),
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable
            })
            LabModulesSummary.Text = "模块 UI 加载失败：" & Ex.Message
            LabModuleUiSummary.Text = "模块 UI 加载失败：" & Ex.Message
        End Try
    End Function

    Private Async Sub ModuleUiEvent_Dispatch(NodeId As String, Action As String, Value As String)
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) OrElse String.IsNullOrWhiteSpace(CurrentModuleId) Then Return
        Try
            Await WsClient.SendUiEventAsync(CurrentBaseUrl, CurrentModuleId, NodeId, Action, Value, CurrentUiKind, CurrentLinkId)
            LabModulesSummary.Text = "已发送模块 UI 事件：" & NodeId & " / " & Action
            LabModuleUiSummary.Text = "已发送模块 UI 事件：" & NodeId & " / " & Action
        Catch Ex As Exception
            LabModuleUiSummary.Text = "模块 UI 事件发送失败：" & Ex.Message
        End Try
    End Sub

    Private Sub BtnModuleUiBack_Click(sender As Object, e As EventArgs) Handles BtnModuleUiBack.Click
        CollapseModuleUi()
    End Sub

    Private Function IsExpandedModule(ModuleId As String) As Boolean
        If String.IsNullOrWhiteSpace(ModuleId) OrElse String.IsNullOrWhiteSpace(ExpandedModuleKey) Then Return False
        Return ExpandedModuleKey.StartsWith(ModuleId & "|", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function BuildModulePanelKey(ModuleId As String, UiKind As String, LinkId As String) As String
        Return If(ModuleId, "") & "|" & If(UiKind, "default") & "|" & If(LinkId, "")
    End Function

    Private Function CreateExpandedModulePanel() As FrameworkElement
        Dim Border As New Border With {
            .CornerRadius = New CornerRadius(8),
            .Padding = New Thickness(14),
            .Margin = New Thickness(0, 0, 0, 8)
        }
        Border.SetResourceReference(Border.BackgroundProperty, "ColorBrushSemiTransparent")

        Dim Panel As New StackPanel()
        Dim Title As New TextBlock With {
            .Text = If(String.IsNullOrWhiteSpace(ExpandedModuleTitle), "模块 UI", ExpandedModuleTitle),
            .FontSize = 13,
            .FontWeight = FontWeights.SemiBold,
            .Margin = New Thickness(0, 0, 0, 10)
        }
        Title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
        Panel.Children.Add(Title)

        If ExpandedModuleLoading Then
            Dim LoadingText As New TextBlock With {.Text = "正在加载...", .FontSize = 12}
            LoadingText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush2")
            Panel.Children.Add(LoadingText)
        ElseIf Not String.IsNullOrWhiteSpace(ExpandedModuleError) Then
            Dim ErrorText As New TextBlock With {.Text = "加载失败：" & ExpandedModuleError, .FontSize = 12, .TextWrapping = TextWrapping.Wrap}
            ErrorText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight")
            Panel.Children.Add(ErrorText)
        ElseIf ExpandedModuleTree IsNot Nothing Then
            Panel.Children.Add(OmniMixRawNodeRenderer.Render(ExpandedModuleTree, CurrentBaseUrl, AddressOf ModuleUiEvent_Dispatch))
        End If

        Border.Child = Panel
        Return Border
    End Function

    Private Sub CollapseModuleUi(Optional RestorePreviousPane As Boolean = True)
        CardModuleUi.Visibility = Visibility.Collapsed
        ModuleUiRequestSerial += 1
        CardModules.Visibility = If(PageKey = "Modules", Visibility.Visible, Visibility.Collapsed)
        PanModuleUi.Children.Clear()
        CurrentModuleId = ""
        CurrentUiKind = "default"
        CurrentLinkId = ""
        ExpandedModuleKey = ""
        ExpandedModuleTree = Nothing
        ExpandedModuleError = ""
        ExpandedModuleLoading = False
        If RestorePreviousPane AndAlso Not String.IsNullOrWhiteSpace(PreviousModulesPaneForDetail) Then
            CurrentModulesPane = PreviousModulesPaneForDetail
        End If
        UpdatePluginPaneVisibility()
        Select Case CurrentModulesPane
            Case "launchpad"
                RenderLaunchpad(CurrentModules)
            Case "game"
                If Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
                    Dim RefreshTask = RefreshModulesAsync(CurrentBaseUrl)
                End If
            Case Else
                RenderModules(CurrentModules)
        End Select
    End Sub

    Private Shared Function ModuleLinkIcon(LinkEntry As OmniMixModuleLinkEntryInfo) As String
        Select Case If(LinkEntry?.Icon, "").Trim().ToLowerInvariant()
            Case "music_note", "headphones", "radio"
                Return IconPlay
            Case "folder"
                Return Logo.IconButtonOpen
            Case "cloud"
                Return Logo.IconButtonRefresh
            Case Else
                Return Logo.IconButtonSetup
        End Select
    End Function

    Private Async Sub ModuleEnableButton_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Dim Button = CType(sender, MyIconButton)
        Dim ModuleInfo = TryCast(Button.Tag, OmniMixModuleInfo)
        If ModuleInfo Is Nothing Then Return

        Dim PreviousValue = ModuleInfo.Enabled
        ModuleInfo.Enabled = Not ModuleInfo.Enabled
        Button.IsEnabled = False
        LabModulesSummary.Text = "正在保存模块状态：" & NonEmpty(ModuleInfo.Name, ModuleInfo.Id)

        Try
            Await OmniMixApiClient.SetModuleEnabledAsync(CurrentBaseUrl, ModuleInfo.Id, ModuleInfo.Enabled)
            LabModulesSummary.Text = "已更新模块“" & NonEmpty(ModuleInfo.Name, ModuleInfo.Id) & "”的启用状态。变更将在后端模块加载策略允许时生效。"
            RenderModules(CurrentModules)
        Catch Ex As Exception
            ModuleInfo.Enabled = PreviousValue
            Button.IsEnabled = True
            LabModulesSummary.Text = "模块启用状态保存失败：" & Ex.Message
        End Try
    End Sub

    Private Async Sub ModuleItem_Changed(sender As Object, e As RouteEventArgs)
        If Not e.RaiseByMouse Then Return
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Dim Item = CType(sender, MyListItem)
        Dim ModuleInfo = CType(Item.Tag, OmniMixModuleInfo)
        Dim PreviousValue = ModuleInfo.Enabled
        ModuleInfo.Enabled = Item.Checked
        Item.Info = BuildModuleInfoText(ModuleInfo) & " · 正在保存..."

        Try
            Await OmniMixApiClient.SetModuleEnabledAsync(CurrentBaseUrl, ModuleInfo.Id, ModuleInfo.Enabled)
            Item.Info = BuildModuleInfoText(ModuleInfo)
            LabModulesSummary.Text = "已更新模块“" & NonEmpty(ModuleInfo.Name, ModuleInfo.Id) & "”的启用状态。变更将在后端模块加载策略允许时生效。"
        Catch Ex As Exception
            ModuleInfo.Enabled = PreviousValue
            Item.Checked = PreviousValue
            Item.Info = BuildModuleInfoText(ModuleInfo)
            LabModulesSummary.Text = "模块启用状态保存失败：" & Ex.Message
        End Try
    End Sub

    Private Shared Function BuildModuleInfoText(ModuleInfo As OmniMixModuleInfo) As String
        Dim Parts As New List(Of String)
        If Not String.IsNullOrWhiteSpace(ModuleInfo.Version) Then Parts.Add("v" & ModuleInfo.Version)
        Parts.Add(GetModuleStatusText(ModuleInfo))
        Parts.Add("优先级 " & ModuleInfo.Priority)
        If ModuleInfo.HasSettingsUI Then Parts.Add("设置 UI")
        If ModuleInfo.HasQuickLinks Then Parts.Add($"{If(ModuleInfo.LinkEntries, New List(Of OmniMixModuleLinkEntryInfo)).Count} 个快捷入口")
        Return String.Join(" · ", Parts)
    End Function

    Private Shared Function GetModuleStatusText(ModuleInfo As OmniMixModuleInfo) As String
        Dim Loaded = Not String.IsNullOrWhiteSpace(ModuleInfo.LoadedAt)
        If Loaded AndAlso ModuleInfo.Enabled Then Return "已加载并激活"
        If Loaded AndAlso Not ModuleInfo.Enabled Then Return "已加载但下次启动将禁用"
        If Not Loaded AndAlso ModuleInfo.Enabled Then Return "下次启动时将加载"
        Return "已禁用"
    End Function

    Private Async Function RefreshLibraryAsync(BaseUrl As String) As Task
        If PageKey <> "Library" Then Return

        LabLibrarySummary.Text = "正在加载曲库..."
        PanLibraryList.Children.Clear()

        Try
            Dim Playlist = Await OmniMixApiClient.GetPlaylistAsync(BaseUrl)
            CurrentLibraryPlaylist = If(Playlist, New OmniMixPlaylistData)
            SetLibraryPane(CurrentLibraryPane)
        Catch Ex As Exception
            LabLibrarySummary.Text = "曲库加载失败：" & Ex.Message
        End Try
    End Function

    Private Sub RenderLibrary()
        Dim Playlist = If(CurrentLibraryPlaylist, New OmniMixPlaylistData)
        Dim Albums = If(Playlist.Albums, New List(Of OmniMixAlbumInfo))
        Dim Songs = If(Playlist.Songs, New List(Of OmniMixSongInfo))
        Dim SelectedModuleId = If(CurrentLibraryPane, "")
        Dim HasSourceFilter = Not String.IsNullOrWhiteSpace(SelectedModuleId)

        PanLibraryList.Children.Clear()

        Dim VisibleSongs = Songs.
            Where(Function(Song) Not HasSourceFilter OrElse String.Equals(Song.ModuleId, SelectedModuleId, StringComparison.OrdinalIgnoreCase)).
            ToList()
        Dim VisibleAlbums = Albums.
            Where(Function(Album)
                      If Not HasSourceFilter Then Return True
                      If String.Equals(Album.ModuleId, SelectedModuleId, StringComparison.OrdinalIgnoreCase) Then Return True
                      Return VisibleSongs.Any(Function(Song) String.Equals(Song.AlbumId, Album.Id, StringComparison.OrdinalIgnoreCase))
                  End Function).
            ToList()
        Dim Groups = BuildLibraryAlbumGroups(VisibleAlbums, VisibleSongs)

        LabLibrarySummary.Text = $"{Groups.Count} 组，{VisibleSongs.Count} 首歌曲。"

        If Groups.Count = 0 Then
            AddLibraryItem("这里还没有歌曲", "换一个来源，或等待对应模块加载完成。", 0)
            Return
        End If

        For Each Group In Groups.OrderBy(Function(Item) Item.Name)
            AddLibraryAlbumItem(Group)
        Next
    End Sub

    Private Sub AddLibraryItem(Title As String, Info As String, Level As Integer)
        PanLibraryList.Children.Add(New MyListItem With {
            .Title = Title,
            .Info = Info,
            .Height = 42,
            .PaddingLeft = 8 + Level * 24,
            .Margin = New Thickness(0, 0, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable
        })
    End Sub

    Private Function BuildLibraryAlbumGroups(Albums As List(Of OmniMixAlbumInfo), Songs As List(Of OmniMixSongInfo)) As List(Of LibraryAlbumGroup)
        Dim Groups As New List(Of LibraryAlbumGroup)
        Dim UsedSongUuids As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each AlbumInfo In If(Albums, New List(Of OmniMixAlbumInfo))
            Dim AlbumSongs = If(Songs, New List(Of OmniMixSongInfo)).
                Where(Function(Song) String.Equals(Song.AlbumId, AlbumInfo.Id, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(Song) NonEmpty(Song.Title, Song.Uuid)).
                ToList()
            For Each SongInfo In AlbumSongs
                If Not String.IsNullOrWhiteSpace(SongInfo.Uuid) Then UsedSongUuids.Add(SongInfo.Uuid)
            Next

            If AlbumSongs.Count = 0 AndAlso AlbumInfo.SongCount <= 0 Then Continue For
            Groups.Add(New LibraryAlbumGroup With {
                .GroupId = "album_" & NonEmpty(AlbumInfo.Id, Guid.NewGuid().ToString("N")),
                .Name = NonEmpty(AlbumInfo.Name, AlbumInfo.Id),
                .ModuleId = AlbumInfo.ModuleId,
                .CoverPath = AlbumInfo.CoverPath,
                .Songs = AlbumSongs
            })
        Next

        Dim LooseSongs = If(Songs, New List(Of OmniMixSongInfo)).
            Where(Function(Song) String.IsNullOrWhiteSpace(Song.Uuid) OrElse Not UsedSongUuids.Contains(Song.Uuid)).
            OrderBy(Function(Song) NonEmpty(Song.Title, Song.Uuid)).
            ToList()
        If LooseSongs.Count > 0 Then
            Groups.Add(New LibraryAlbumGroup With {
                .GroupId = "loose_" & NonEmpty(LooseSongs.First().ModuleId, "unknown"),
                .Name = "未分组",
                .ModuleId = NonEmpty(LooseSongs.First().ModuleId, ""),
                .Songs = LooseSongs
            })
        End If

        Return Groups
    End Function

    Private Sub AddLibraryAlbumItem(Group As LibraryAlbumGroup)
        Dim IsExpanded = ExpandedLibraryAlbums.Contains(Group.GroupId)
        Dim SongUuids = Group.Songs.Select(Function(Song) Song.Uuid).Where(Function(Uuid) Not String.IsNullOrWhiteSpace(Uuid)).ToList()
        Dim Item As New MyListItem With {
            .Title = NonEmpty(Group.Name, Group.GroupId),
            .Info = $"{Group.Songs.Count} 首{If(String.IsNullOrWhiteSpace(Group.ModuleId), "", " · " & Group.ModuleId)}",
            .Height = 68,
            .PaddingLeft = 8,
            .Logo = ResolveLibraryAlbumCover(Group),
            .LogoWidth = 60,
            .LogoScale = 1,
            .Margin = New Thickness(0, 0, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable
        }

        Dim ExpandButton As New MyIconButton With {
            .Logo = If(IsExpanded, IconExpandUp, IconExpandDown),
            .LogoScale = 0.8,
            .ToolTip = If(IsExpanded, "收起", "展开"),
            .Tag = Group.GroupId
        }
        AddHandler ExpandButton.Click, AddressOf LibraryAlbumExpandButton_Click

        Dim QueueButton As New MyIconButton With {
            .Logo = IconPlus,
            .LogoScale = 0.82,
            .ToolTip = "加入队列",
            .Tag = New LibraryQueueGroupPayload With {
                .GroupId = Group.GroupId,
                .Name = Group.Name,
                .Uuids = SongUuids
            }
        }
        AddHandler QueueButton.Click, AddressOf LibrarySourceButton_Click
        Item.Buttons = New List(Of MyIconButton) From {ExpandButton, QueueButton}
        PanLibraryList.Children.Add(Item)

        If IsExpanded Then
            For Each SongInfo In Group.Songs
                Dim InfoParts As New List(Of String)
                If Not String.IsNullOrWhiteSpace(SongInfo.Artist) Then InfoParts.Add(SongInfo.Artist)
                If SongInfo.Duration > 0 Then InfoParts.Add(FormatDuration(SongInfo.Duration))
                AddLibrarySongItem(SongInfo, String.Join(" · ", InfoParts), Group)
            Next
        End If
    End Sub

    Private Sub AddLibrarySongItem(SongInfo As OmniMixSongInfo, Info As String, Group As LibraryAlbumGroup)
        Dim CapturedSongInfo = SongInfo
        Dim CapturedInfo = Info
        Dim CapturedGroup = Group
        PanLibraryList.Children.Add(New MyVirtualizingElement(Of MyListItem)(
            Function() CreateLibrarySongItem(CapturedSongInfo, CapturedInfo, CapturedGroup)
        ) With {.Height = 64})
    End Sub

    Private Function CreateLibrarySongItem(SongInfo As OmniMixSongInfo, Info As String, Group As LibraryAlbumGroup) As MyListItem
        Dim Item As New MyListItem With {
            .Title = NonEmpty(SongInfo.Title, SongInfo.Uuid),
            .Info = Info,
            .Height = 64,
            .PaddingLeft = 32,
            .Logo = ResolveLibrarySongCover(SongInfo, Group),
            .LogoWidth = 58,
            .LogoScale = 1,
            .Margin = New Thickness(0, 0, 0, 2),
            .IsScaleAnimationEnabled = False,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = SongInfo
        }

        Dim QueueButton As New MyIconButton With {
            .Logo = IconPlus,
            .LogoScale = 0.82,
            .ToolTip = "加入队列",
            .Tag = SongInfo
        }
        AddHandler QueueButton.Click, AddressOf LibrarySongQueueButton_Click
        Item.Buttons = New List(Of MyIconButton) From {QueueButton}
        AddHandler Item.Click, AddressOf LibrarySongItem_Click
        Return Item
    End Function

    Private Async Sub LibrarySongItem_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Item = CType(sender, MyListItem)
        Dim SongInfo = TryCast(Item.Tag, OmniMixSongInfo)
        If SongInfo Is Nothing OrElse String.IsNullOrWhiteSpace(SongInfo.Uuid) Then Return
        Await PlayLibrarySongAsync(SongInfo)
    End Sub

    Private Sub LibraryAlbumExpandButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim GroupId = TryCast(Button.Tag, String)
        If String.IsNullOrWhiteSpace(GroupId) Then Return

        If ExpandedLibraryAlbums.Contains(GroupId) Then
            ExpandedLibraryAlbums.Remove(GroupId)
        Else
            ExpandedLibraryAlbums.Add(GroupId)
        End If
        RenderLibrary()
    End Sub

    Private Async Sub LibrarySongQueueButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim SongInfo = TryCast(Button.Tag, OmniMixSongInfo)
        If SongInfo Is Nothing OrElse String.IsNullOrWhiteSpace(SongInfo.Uuid) Then Return
        Await AddLibrarySongToQueueAsync(SongInfo)
    End Sub

    Private Async Sub LibrarySourceButton_Click(sender As Object, e As EventArgs)
        Dim Button = CType(sender, MyIconButton)
        Dim Payload = TryCast(Button.Tag, LibraryQueueGroupPayload)
        If Payload Is Nothing Then Return
        Await AddLibraryGroupToQueueAsync(Payload)
    End Sub

    Private Async Function AddLibraryGroupToQueueAsync(Payload As LibraryQueueGroupPayload) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Dim Uuids = If(Payload.Uuids, New List(Of String)).
            Where(Function(Uuid) Not String.IsNullOrWhiteSpace(Uuid)).
            Distinct().
            ToList()
        If Uuids.Count = 0 Then
            LabLibrarySummary.Text = "没有可加入队列的歌曲。"
            Return
        End If

        Try
            Dim Instance = Await GetControllableInstanceAsync()
            If Instance Is Nothing Then
                LabLibrarySummary.Text = "没有可控制的播放实例，暂时无法加入队列。"
                Return
            End If

            Await OmniMixApiClient.AddToQueueRangeAsync(CurrentBaseUrl, Instance.Id, Uuids)
            LabLibrarySummary.Text = $"已加入队列：{NonEmpty(Payload.Name, Payload.GroupId)}（{Uuids.Count} 首）"
        Catch Ex As Exception
            LabLibrarySummary.Text = "加入队列失败：" & Ex.Message
        End Try
    End Function

    Private Async Function PlayLibrarySongAsync(SongInfo As OmniMixSongInfo) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Try
            Dim Instance = Await GetControllableInstanceAsync()
            If Instance Is Nothing Then
                LabLibrarySummary.Text = "没有可控制的播放实例，暂时无法播放歌曲。"
                Return
            End If

            Await OmniMixApiClient.PlayAsync(CurrentBaseUrl, Instance.Id, SongInfo.Uuid)
            LabLibrarySummary.Text = "已发送播放指令：" & NonEmpty(SongInfo.Title, SongInfo.Uuid)
        Catch Ex As Exception
            LabLibrarySummary.Text = "播放失败：" & Ex.Message
        End Try
    End Function

    Private Async Function AddLibrarySongToQueueAsync(SongInfo As OmniMixSongInfo) As Task
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return

        Try
            Dim Instance = Await GetControllableInstanceAsync()
            If Instance Is Nothing Then
                LabLibrarySummary.Text = "没有可控制的播放实例，暂时无法加入队列。"
                Return
            End If

            Await OmniMixApiClient.AddToQueueAsync(CurrentBaseUrl, Instance.Id, SongInfo.Uuid)
            LabLibrarySummary.Text = "已加入队列：" & NonEmpty(SongInfo.Title, SongInfo.Uuid)
        Catch Ex As Exception
            LabLibrarySummary.Text = "加入队列失败：" & Ex.Message
        End Try
    End Function

    Private Async Function GetControllableInstanceAsync() As Task(Of OmniMixPlaybackInstanceInfo)
        If String.IsNullOrWhiteSpace(CurrentBaseUrl) Then Return Nothing

        Dim Instances = Await OmniMixApiClient.GetInstancesAsync(CurrentBaseUrl)
        If Instances Is Nothing OrElse Instances.Count = 0 Then Return Nothing

        Dim Config = Await OmniMixApiClient.GetConfigAsync(CurrentBaseUrl)
        Dim ActiveId = ConfigString(Config, "active_instance", "")
        Dim Active = If(String.IsNullOrWhiteSpace(ActiveId), Nothing, Instances.FirstOrDefault(Function(Instance) Instance.Id = ActiveId AndAlso Instance.IsServerManaged))
        If Active IsNot Nothing Then Return Active

        Return PickActiveInstance(Instances)
    End Function

    Private Function PromptEqualizerNumber(Title As String, Description As String, DefaultValue As Double, MinValue As Double, MaxValue As Double, ByRef Result As Double) As Boolean
        Dim Text = MyMsgBoxInput(
            Title,
            Description & vbCrLf & "范围：" & MinValue.ToString("0.###", CultureInfo.InvariantCulture) & " - " & MaxValue.ToString("0.###", CultureInfo.InvariantCulture),
            DefaultValue.ToString("0.###", CultureInfo.InvariantCulture),
            HintText:="数字")
        If Text Is Nothing Then Return False

        If Not TryParseEqualizerNumber(Text, Result) Then
            LabEqualizerSummary.Text = "输入的数字无效。"
            Return False
        End If
        Result = Clamp(Result, MinValue, MaxValue)
        Return True
    End Function

    Private Function PromptEqualizerType(DefaultType As String) As String
        Dim Text = MyMsgBoxInput(
            "滤波类型",
            "输入滤波类型：Peaking、LowShelf、HighShelf、LowPass、HighPass。",
            NonEmpty(NormalizeEqualizerType(DefaultType), "Peaking"),
            HintText:="Peaking / LowShelf / HighShelf / LowPass / HighPass")
        If Text Is Nothing Then Return Nothing

        Dim TypeValue = NormalizeEqualizerType(Text)
        If String.IsNullOrWhiteSpace(TypeValue) Then
            LabEqualizerSummary.Text = "滤波类型无效。"
            Return Nothing
        End If
        Return TypeValue
    End Function

    Private Shared Function TryParseEqualizerNumber(Text As String, ByRef Value As Double) As Boolean
        If Double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, Value) Then Return True
        Return Double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, Value)
    End Function

    Private Shared Function NormalizeEqualizerState(State As OmniMixEqualizerStateInfo) As OmniMixEqualizerStateInfo
        State = If(State, New OmniMixEqualizerStateInfo)
        State.Points = If(State.Points, New List(Of OmniMixEqualizerPointInfo))
        For Each Point In State.Points
            If String.IsNullOrWhiteSpace(Point.Id) Then Point.Id = "vb_" & Guid.NewGuid().ToString("N")
            Point.Frequency = Clamp(Point.Frequency, 20, 22000)
            Point.GainDb = Clamp(Point.GainDb, -24, 24)
            Point.Q = Clamp(Point.Q, 0.1, 20)
            Dim TypeValue = NormalizeEqualizerType(Point.Type)
            Point.Type = If(String.IsNullOrWhiteSpace(TypeValue), "Peaking", TypeValue)
        Next
        State.GlobalGainDb = Clamp(State.GlobalGainDb, -24, 24)
        Return State
    End Function

    Private Shared Function CloneEqualizerState(State As OmniMixEqualizerStateInfo) As OmniMixEqualizerStateInfo
        State = NormalizeEqualizerState(State)
        Return New OmniMixEqualizerStateInfo With {
            .Enabled = State.Enabled,
            .GlobalGainDb = State.GlobalGainDb,
            .SoftClipEnabled = State.SoftClipEnabled,
            .Points = State.Points.Select(Function(Point) New OmniMixEqualizerPointInfo With {
                .Id = Point.Id,
                .Frequency = Point.Frequency,
                .GainDb = Point.GainDb,
                .Q = Point.Q,
                .Type = Point.Type
            }).ToList()
        }
    End Function

    Private Shared Function BuildEqualizerStateInfo(State As OmniMixEqualizerStateInfo) As String
        State = NormalizeEqualizerState(State)
        Return $"{If(State.Enabled, "启用", "禁用")} · 全局增益 {FormatDb(State.GlobalGainDb)} · {If(State.SoftClipEnabled, "软削波开启", "软削波关闭")} · {State.Points.Count} 个控制点"
    End Function

    Private Shared Function NormalizeEqualizerType(Value As String) As String
        Dim Key = If(Value, "").Trim().Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant()
        Select Case Key
            Case "peaking", "peak", "bell"
                Return "Peaking"
            Case "lowshelf", "bass"
                Return "LowShelf"
            Case "highshelf", "treble"
                Return "HighShelf"
            Case "lowpass", "lp"
                Return "LowPass"
            Case "highpass", "hp"
                Return "HighPass"
            Case Else
                Return ""
        End Select
    End Function

    Private Shared Function GetEqualizerTypeLabel(TypeValue As String) As String
        Select Case NormalizeEqualizerType(TypeValue)
            Case "LowShelf"
                Return "低架"
            Case "HighShelf"
                Return "高架"
            Case "LowPass"
                Return "低通"
            Case "HighPass"
                Return "高通"
            Case Else
                Return "峰值"
        End Select
    End Function

    Private Shared Function FormatFrequency(Frequency As Double) As String
        If Frequency >= 1000 Then Return (Frequency / 1000).ToString("0.#", CultureInfo.InvariantCulture) & " kHz"
        Return Frequency.ToString("0", CultureInfo.InvariantCulture) & " Hz"
    End Function

    Private Shared Function FormatDb(Value As Double) As String
        Dim Prefix = If(Value > 0, "+", "")
        Return Prefix & Value.ToString("0.0", CultureInfo.InvariantCulture) & " dB"
    End Function

    Private Shared Function Clamp(Value As Double, MinValue As Double, MaxValue As Double) As Double
        Return Math.Max(MinValue, Math.Min(MaxValue, Value))
    End Function

    Private Shared Function NonEmpty(Value As String, Fallback As String) As String
        Return If(String.IsNullOrWhiteSpace(Value), Fallback, Value)
    End Function

    Private Function ResolveQueueItemCover(Track As OmniMixTrackInfo) As String
        If Track Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(Track.Uuid) AndAlso Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Return CurrentBaseUrl.TrimEnd("/"c) & "/api/track/cover?uuid=" & Uri.EscapeDataString(Track.Uuid)
        End If
        For Each Candidate In {Track.CoverPath, Track.CoverUrl, Track.ImageUrl}
            If String.IsNullOrWhiteSpace(Candidate) Then Continue For
            If Candidate.StartsWith("/") AndAlso Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
                Return CurrentBaseUrl.TrimEnd("/"c) & Candidate
            End If
            Return Candidate
        Next
        Return ""
    End Function

    Private Function ResolveLibraryAlbumCover(Group As LibraryAlbumGroup) As String
        If Group Is Nothing Then Return ""
        For Each SongInfo In If(Group.Songs, New List(Of OmniMixSongInfo))
            Dim SongCover = ResolveLibrarySongCover(SongInfo, Group)
            If Not String.IsNullOrWhiteSpace(SongCover) Then Return SongCover
        Next
        Return ResolveCoverCandidate(Group.CoverPath)
    End Function

    Private Function ResolveLibrarySongCover(SongInfo As OmniMixSongInfo, Group As LibraryAlbumGroup) As String
        If SongInfo Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(SongInfo.Uuid) AndAlso Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Return CurrentBaseUrl.TrimEnd("/"c) & "/api/track/cover?uuid=" & Uri.EscapeDataString(SongInfo.Uuid)
        End If

        For Each Candidate In {SongInfo.CoverPath, SongInfo.CoverUrl, SongInfo.ImageUrl, If(Group Is Nothing, "", Group.CoverPath)}
            Dim Resolved = ResolveCoverCandidate(Candidate)
            If Not String.IsNullOrWhiteSpace(Resolved) Then Return Resolved
        Next
        Return ""
    End Function

    Private Function ResolveCoverCandidate(Candidate As String) As String
        If String.IsNullOrWhiteSpace(Candidate) Then Return ""
        If Candidate.StartsWith("/") AndAlso Not String.IsNullOrWhiteSpace(CurrentBaseUrl) Then
            Return CurrentBaseUrl.TrimEnd("/"c) & Candidate
        End If
        Return Candidate
    End Function

    Private Shared Function ConfigString(Config As Dictionary(Of String, JsonElement), Key As String, Fallback As String) As String
        If Config Is Nothing OrElse Not Config.ContainsKey(Key) Then Return Fallback
        Dim Value = Config(Key)
        Select Case Value.ValueKind
            Case JsonValueKind.String
                Return NonEmpty(Value.GetString(), Fallback)
            Case JsonValueKind.Number, JsonValueKind.True, JsonValueKind.False
                Return Value.ToString()
            Case Else
                Return Fallback
        End Select
    End Function

    Private Shared Function ConfigBoolean(Config As Dictionary(Of String, JsonElement), Key As String, Fallback As Boolean) As Boolean
        If Config Is Nothing OrElse Not Config.ContainsKey(Key) Then Return Fallback
        Dim Value = Config(Key)
        Select Case Value.ValueKind
            Case JsonValueKind.True
                Return True
            Case JsonValueKind.False
                Return False
            Case JsonValueKind.String
                Dim Parsed As Boolean
                If Boolean.TryParse(Value.GetString(), Parsed) Then Return Parsed
                Return Fallback
            Case Else
                Return Fallback
        End Select
    End Function

    Private Shared Function ArchiveTicks(ArchivedAt As String) As Long
        Dim Parsed As DateTimeOffset
        If DateTimeOffset.TryParse(ArchivedAt, Parsed) Then Return Parsed.UtcTicks
        Return 0
    End Function

    Private Shared Function FormatArchiveTime(ArchivedAt As String) As String
        Dim Parsed As DateTimeOffset
        If DateTimeOffset.TryParse(ArchivedAt, Parsed) Then Return Parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        Return ArchivedAt
    End Function

    Private Shared Function FormatDuration(Seconds As Double) As String
        If Seconds <= 0 Then Return ""
        Dim TotalSeconds = CInt(Math.Floor(Seconds))
        Return $"{TotalSeconds \ 60}:{(TotalSeconds Mod 60).ToString().PadLeft(2, "0"c)}"
    End Function
End Class
