Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        If IsLoaded Then Return
        IsLoaded = True
    End Sub

End Class
