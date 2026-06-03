#Region "附加属性"

''' <summary>
''' 用于在 XAML 中初始化列表对象。
''' </summary>
<Markup.ContentProperty("Events")>
Public Class CustomEventCollection
    Implements IEnumerable(Of CustomEvent)

    Private ReadOnly _Events As New List(Of CustomEvent)
    Public ReadOnly Property Events As List(Of CustomEvent)
        Get
            Return _Events
        End Get
    End Property

    Public Function GetEnumerator() As IEnumerator(Of CustomEvent) Implements IEnumerable(Of CustomEvent).GetEnumerator
        Return DirectCast(Events, IEnumerable(Of CustomEvent)).GetEnumerator()
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return DirectCast(Events, IEnumerable).GetEnumerator()
    End Function
End Class

''' <summary>
''' 提供自定义事件的附加属性。
''' </summary>
Public Class CustomEventService

    Public Shared ReadOnly EventsProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("Events", GetType(CustomEventCollection), GetType(CustomEventService), New PropertyMetadata(Nothing))

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEvents(d As DependencyObject, value As CustomEventCollection)
        d.SetValue(EventsProperty, value)
    End Sub

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEvents(d As DependencyObject) As CustomEventCollection
        If d.GetValue(EventsProperty) Is Nothing Then d.SetValue(EventsProperty, New CustomEventCollection)
        Return d.GetValue(EventsProperty)
    End Function

    Public Shared ReadOnly EventTypeProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("EventType", GetType(CustomEvent.EventType), GetType(CustomEventService), New PropertyMetadata(CustomEvent.EventType.None))

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventType(d As DependencyObject, value As CustomEvent.EventType)
        d.SetValue(EventTypeProperty, value)
    End Sub

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventType(d As DependencyObject) As CustomEvent.EventType
        Return d.GetValue(EventTypeProperty)
    End Function

    Public Shared ReadOnly EventDataProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("EventData", GetType(String), GetType(CustomEventService), New PropertyMetadata(Nothing))

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventData(d As DependencyObject, value As String)
        d.SetValue(EventDataProperty, value)
    End Sub

    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventData(d As DependencyObject) As String
        Return d.GetValue(EventDataProperty)
    End Function

End Class

Partial Public Module ModMain

    ''' <summary>
    ''' 触发该控件上的自定义事件。
    ''' </summary>
    <Runtime.CompilerServices.Extension>
    Public Sub RaiseCustomEvent(Control As DependencyObject)
        Dim Events = CustomEventService.GetEvents(Control).ToList
        Dim EventType = CustomEventService.GetEventType(Control)
        If EventType <> CustomEvent.EventType.None Then Events.Add(New CustomEvent(EventType, CustomEventService.GetEventData(Control)))
        If Not Events.Any Then Return
        RunInNewThread(
        Sub()
            For Each e In Events
                e.Raise()
            Next
        End Sub, "执行自定义事件 " & GetUuid())
    End Sub

End Module

#End Region

''' <summary>
''' OmniMix 前端保留的通用自定义事件。
''' </summary>
Public Class CustomEvent
    Inherits DependencyObject

    Public Property Type As EventType
        Get
            Dim Value As EventType = EventType.None
            RunInUiWait(Sub() Value = GetValue(TypeProperty))
            Return Value
        End Get
        Set(value As EventType)
            SetValue(TypeProperty, value)
        End Set
    End Property

    Public Shared ReadOnly TypeProperty As DependencyProperty =
        DependencyProperty.Register("Type", GetType(EventType), GetType(CustomEvent), New PropertyMetadata(EventType.None))

    Public Property Data As String
        Get
            Dim Value As String = Nothing
            RunInUiWait(Sub() Value = GetValue(DataProperty))
            Return Value
        End Get
        Set(value As String)
            SetValue(DataProperty, value)
        End Set
    End Property

    Public Shared ReadOnly DataProperty As DependencyProperty =
        DependencyProperty.Register("Data", GetType(String), GetType(CustomEvent), New PropertyMetadata(Nothing))

    Public Sub New()
    End Sub

    Public Sub New(Type As EventType, Data As String)
        Me.Type = Type
        Me.Data = Data
    End Sub

    Public Sub Raise()
        Raise(Type, Data)
    End Sub

    Public Enum EventType
        None = 0
        打开网页
        打开文件
        打开帮助
        执行命令
        复制文本
        刷新页面
        弹出窗口
        弹出提示
        修改设置
        写入设置
        修改变量
        写入变量
        检查更新
    End Enum

    Public Shared Sub Raise(Type As EventType, Arg As String)
        If Type = EventType.None Then Return
        Logger.Info($"执行自定义事件：{Type}, {Arg}")
        If Arg Is Nothing Then Arg = ""
        Dim Args As String() = Arg.Split("|")
        Try
            Select Case Type
                Case EventType.打开网页
                    Arg = Arg.Replace("\", "/")
                    If Not Arg.Contains("://") OrElse Arg.StartsWithF("file", True) Then
                        MyMsgBox("EventData 必须为一个网址。", "事件执行失败")
                        Return
                    End If
                    Hint("正在开启，请稍候：" & Arg)
                    RunInThread(Sub() OpenWebsite(Arg))

                Case EventType.打开文件, EventType.打开帮助, EventType.执行命令
                    RunInThread(
                    Sub()
                        Try
                            Dim ActualPaths = GetAbsoluteUrls(Args(0), Type)
                            If Not EventSafetyConfirm("即将执行：" & ActualPaths(0) & If(Args.Length >= 2, " " & Args(1), "")) Then Return
                            StartProcess(New ProcessStartInfo With {
                                .Arguments = If(Args.Length >= 2, Args(1), ""),
                                .FileName = ActualPaths(0),
                                .WorkingDirectory = ActualPaths(1)
                            })
                        Catch ex As Exception
                            Logger.Error(ex, "执行打开类自定义事件失败", LogBehavior.Alert)
                        End Try
                    End Sub)

                Case EventType.复制文本
                    ClipboardSet(Arg)

                Case EventType.刷新页面
                    If TypeOf FrmMain.PageRight Is IRefreshable Then
                        RunInUiWait(Sub() CType(FrmMain.PageRight, IRefreshable).Refresh())
                        If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)
                    Else
                        Hint("当前页面不支持刷新操作！", HintType.Red)
                    End If

                Case EventType.弹出窗口
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 弹窗标题|弹窗内容")
                    MyMsgBox(Args(1).Replace("\n", vbCrLf), Args(0).Replace("\n", vbCrLf), If(Args.Length > 2, Args(2), "确定"))

                Case EventType.弹出提示
                    Hint(Args(0).Replace("\n", vbCrLf), If(Args.Length = 1, HintType.Blue, Args(1).ToEnum(Of HintType)))

                Case EventType.修改设置, EventType.写入设置
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 UiLauncherTransparent|400")
                    Settings.SetSafe(Args(0), Args(1))
                    If Args.Length = 2 Then Hint($"已写入设置：{Args(0)} -> {Args(1)}", HintType.Green)

                Case EventType.修改变量, EventType.写入变量
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 VariableName|SomeValue")
                    WriteReg("CustomEvent" & Args(0), Args(1))
                    If Args.Length = 2 Then Hint($"已写入变量：{Args(0)} -> {Args(1)}", HintType.Green)

                Case EventType.检查更新
                    UpdateCheckByButton()
            End Select
        Catch ex As Exception
            Logger.Error(ex, $"事件执行失败（{Type}, {Arg}）", LogBehavior.Alert)
        End Try
    End Sub

    Public Shared Function GetAbsoluteUrls(RelativeUrl As String, Type As EventType) As String()
        RelativeUrl = RelativeUrl.Replace("/", "\").TrimStart("\"c)

        Dim Location As String
        Dim WorkingDir As String = PathExeFolder
        If RelativeUrl.Contains(":\") Then
            Location = RelativeUrl
            WorkingDir = PathUtils.RemoveLastPart(Location)
        ElseIf FileUtils.Exists(PathExeFolder & RelativeUrl) Then
            Location = PathExeFolder & RelativeUrl
            WorkingDir = PathUtils.RemoveLastPart(Location)
        Else
            Location = RelativeUrl
        End If

        Return {Location, WorkingDir}
    End Function

    Private Shared Function EventSafetyConfirm(Message As String) As Boolean
        If Settings.Get(Of Boolean)("HintCustomCommand") Then Return True
        Select Case MyMsgBox(Message & vbCrLf & "请在确认没有安全隐患后再继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消")
            Case 1
                Return True
            Case 2
                Settings.Set("HintCustomCommand", True)
                Return True
            Case Else
                Return False
        End Select
    End Function

End Class
