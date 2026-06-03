Imports OmniMixFrontend.MyLoading

Public Class MyLoading

    Public Event IsErrorChanged(sender As Object, isError As Boolean)
    Public Event StateChanged(sender As Object, newState As MyLoadingState, oldState As MyLoadingState)
    Public Event Click(sender As Object, e As MouseButtonEventArgs)

    Public Property AutoRun As Boolean = True
    Private Uuid As Integer = GetUuid()

#Region "��ɫ"

    Public Property Foreground As SolidColorBrush
        Get
            Return GetValue(ForegroundProperty)
        End Get
        Set(value As SolidColorBrush)
            SetValue(ForegroundProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ForegroundProperty As DependencyProperty = DependencyProperty.Register("Foreground", GetType(SolidColorBrush), GetType(MyLoading))
    Public Sub New()
        InitializeComponent()
        SetResourceReference(ForegroundProperty, "ColorBrush3")
    End Sub

#End Region

#Region "�ı�"

    Private Property _ShowProgress As Boolean = False
    Public Property ShowProgress As Boolean
        Get
            Return _ShowProgress
        End Get
        Set(value As Boolean)
            If _ShowProgress = value Then Return
            _ShowProgress = value
            RefreshText()
        End Set
    End Property

    Private _Text As String = "������"
    Public Property Text As String
        Get
            Return _Text
        End Get
        Set(value As String)
            _Text = value
            RefreshText()
        End Set
    End Property

    Private _TextError As String = "����ʧ��"
    Public Property TextError As String
        Get
            Return _TextError
        End Get
        Set(value As String)
            _TextError = value
            RefreshText()
        End Set
    End Property
    ''' <summary>
    ''' �Ƿ���ʹ�� Loader ʱʹ�� Loader �Ĵ���������滻Ĭ�ϵĴ����ı���ʾ��
    ''' </summary>
    Public Property TextErrorInherit As Boolean = True

    Private Sub RefreshText() Handles Me.IsErrorChanged, Me.Loaded, _State.ProgressChanged
        RunInUi(
        Sub()
            If InnerState = MyLoadingState.Error Then
                If TextErrorInherit AndAlso State.IsLoader Then
                    Dim Ex As Exception = CType(State, Object).Error
                    If Ex Is Nothing Then
                        LabText.Text = "δ֪����"
                    Else
                        Ex = Ex.RootException()
                        LabText.Text = If(Ex.IsBadNetwork(), "���绷�����ѣ����Ժ����ԣ���ʹ�� VPN �������绷��", Ex.Message)
                    End If
                Else
                    LabText.Text = TextError
                End If
            Else
                If ShowProgress AndAlso State.IsLoader Then
                    LabText.Text = Text & " - " & Math.Floor(CType(State, Object).Progress * 100) & "%"
                Else
                    LabText.Text = Text
                End If
            End If
        End Sub)
    End Sub

#End Region

#Region "״̬�ı�"

    '״̬ö��
    Public Enum MyLoadingState
        Unloaded = -1
        Run = 0
        [Stop] = 1
        [Error] = 2
    End Enum

    '�����ⲿ�ı�Ĺ���״̬
    Private WithEvents _State As ILoadingTrigger
    Public Property State As ILoadingTrigger
        Get
            InitState()
            Return _State
        End Get
        Set(value As ILoadingTrigger)
            _State = value
            RefreshState()
        End Set
    End Property
    Private Sub InitState() Handles Me.Loaded
        If _State Is Nothing Then
            _State = New MyLoadingStateSimulator
            If AutoRun Then _State.LoadingState = MyLoadingState.Run
        End If
    End Sub
    Private Sub RefreshState() Handles _State.LoadingStateChanged, Me.Loaded, Me.Unloaded
        If _State.LoadingState = MyLoadingState.Run AndAlso Not IsLoaded Then InnerState = MyLoadingState.Stop
        InnerState = _State.LoadingState
        OuterState = _State.LoadingState
        AniLoop()
    End Sub

    '���������ⲿ�¼���״̬
    Private Property _OuterState As MyLoadingState = MyLoadingState.Unloaded
    Private Property OuterState As MyLoadingState
        Get
            Return _OuterState
        End Get
        Set(value As MyLoadingState)
            If _OuterState = value Then Return
            Dim OldValue = _OuterState
            _OuterState = value
            '�����¼�
            RaiseEvent StateChanged(Me, value, OldValue)
            If (OldValue = MyLoadingState.Error) <> (value = MyLoadingState.Error) Then RaiseEvent IsErrorChanged(Me, value = MyLoadingState.Error)
        End Set
    End Property


    '���������ڲ������¼���״̬
    Private Property _InnerState As MyLoadingState = MyLoadingState.Unloaded
    Private Property InnerState As MyLoadingState
        Get
            Return _InnerState
        End Get
        Set(value As MyLoadingState)
            If _InnerState = value Then Return
            Dim OldValue = _InnerState
            _InnerState = value
            '�����¼�
            AniLoop()
            If (OldValue = MyLoadingState.Error) <> (value = MyLoadingState.Error) Then ErrorAnimation(Me, value = MyLoadingState.Error)
        End Set
    End Property

#End Region

#Region "����"

    ''' <summary>
    ''' �Ƿ���Ҫ������
    ''' </summary>
    Public Property HasAnimation As Boolean = True

    ''' <summary>
    ''' ������ѭ���Ƿ����������С�
    ''' </summary>
    Private IsLooping As Boolean = False
    Private Sub AniLoop()
        '����ѭ������Ҳ����ʺ���ˣ��Ⱦ�.jpg
        If Not HasAnimation OrElse IsLooping OrElse Not InnerState = MyLoadingState.Run OrElse AniSpeed > 10 OrElse Not IsLoaded Then Return
        IsLooping = True
        ErrorAnimationWaiting = True
        AniStart({
            AaRotateTransform(PathPickaxe, -20 - CType(PathPickaxe.RenderTransform, RotateTransform).Angle, 350, 250, New AniEaseInBack(AniEasePower.Weak)),
            AaRotateTransform(PathPickaxe, 50, 900,, New AniEaseOutFluent, True),
            AaRotateTransform(PathPickaxe, 25, 900,, New AniEaseOutElastic(AniEasePower.Weak)),
            AaCode(
            Sub()
                PathLeft.Opacity = 1
                PathLeft.Margin = New Thickness(7, 41, 0, 0)
                PathRight.Opacity = 1
                PathRight.Margin = New Thickness(14, 41, 0, 0)
                ErrorAnimationWaiting = False
            End Sub),
            AaOpacity(PathLeft, -1, 100, 50),
            AaX(PathLeft, -5, 180,, New AniEaseOutFluent),
            AaY(PathLeft, -6, 180,, New AniEaseOutFluent),
            AaOpacity(PathRight, -1, 100, 50),
            AaX(PathRight, 5, 180,, New AniEaseOutFluent),
            AaY(PathRight, -6, 180,, New AniEaseOutFluent),
            AaCode(
            Sub()
                IsLooping = False
                AniLoop()
            End Sub,, True)
        }, "MyLoader Loop " & Uuid & "/" & GetUuid())
    End Sub

    ''' <summary>
    ''' �����Ƿ�û����ȥ��Ҫ����󶯻��ȴ���
    ''' </summary>
    Private ErrorAnimationWaiting As Boolean = False
    Private Sub ErrorAnimation(sender As Object, isError As Boolean)
        If isError Then
            '�Ǵ����Ϊ����
            Dim Wait As Integer = If(ErrorAnimationWaiting, 400, 0)
            AniStart({
                AaColor(PanBack, ForegroundProperty, "ColorBrushRedLight", 300),
                AaOpacity(PathError, 1 - PathError.Opacity, 100, 300 + Wait),
                AaScaleTransform(PathError, 1 - CType(PathError.RenderTransform, ScaleTransform).ScaleX, 400, 300 + Wait, New AniEaseOutBack)
            }, "MyLoader Error " & Uuid)
        Else
            '�����Ϊ�Ǵ���
            AniStart({
                AaOpacity(PathError, -PathError.Opacity, 100),
                AaScaleTransform(PathError, 0.5 - CType(PathError.RenderTransform, ScaleTransform).ScaleX, 200),
                AaColor(PanBack, ForegroundProperty, "ColorBrush3", 300)
            }, "MyLoader Error " & Uuid)
        End If
    End Sub

#End Region

#Region "����¼�"

    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        RaiseEvent Click(sender, e)
    End Sub
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        '������ж�����ط��ڵ���¼�֮����ʹ�� Button_MouseUp ���� Button_MouseLeave ִ�У�
        IsMouseDown = True
    End Sub
    Private Sub Button_MouseLeave(sender As Object, e As Object) Handles Me.MouseLeave, Me.MouseLeftButtonUp
        IsMouseDown = False
    End Sub

#End Region

End Class

Public Interface ILoadingTrigger
    ReadOnly Property IsLoader As Boolean
    Property LoadingState As MyLoadingState
    Event LoadingStateChanged(NewState As MyLoadingState, OldState As MyLoadingState)
    Event ProgressChanged(NewProgress As Double, OldProgress As Double)
End Interface

Public Class MyLoadingStateSimulator
    Implements ILoadingTrigger
    Private Property _LoadingState As MyLoadingState = MyLoadingState.Unloaded
    Public Property LoadingState As MyLoadingState Implements ILoadingTrigger.LoadingState
        Get
            Return _LoadingState
        End Get
        Set(value As MyLoadingState)
            If _LoadingState = value Then Return
            Dim OldState = _LoadingState
            _LoadingState = value
            RaiseEvent LoadingStateChanged(value, OldState)
        End Set
    End Property
    Public ReadOnly Property IsLoader As Boolean = False Implements ILoadingTrigger.IsLoader

    Public Event LoadingStateChanged(NewState As MyLoadingState, OldState As MyLoadingState) Implements ILoadingTrigger.LoadingStateChanged
    Public Event ProgressChanged(NewProgress As Double, OldProgress As Double) Implements ILoadingTrigger.ProgressChanged
End Class
