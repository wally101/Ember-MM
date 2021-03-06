﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ################################################################################

Imports System.Text.RegularExpressions
Imports EmberAPI

Namespace YouTube

    Public Class Scraper

#Region "Fields"

        Friend WithEvents bwYT As New System.ComponentModel.BackgroundWorker

        Private _VideoLinks As VideoLinkItemCollection

#End Region 'Fields

#Region "Events"

        Public Event Exception(ByVal ex As Exception)

        Public Event VideoLinksRetrieved(ByVal bSuccess As Boolean)

#End Region 'Events

#Region "Properties"

        Public ReadOnly Property VideoLinks() As VideoLinkItemCollection
            Get
                If _VideoLinks Is Nothing Then
                    _VideoLinks = New VideoLinkItemCollection
                End If
                Return _VideoLinks
            End Get
        End Property

#End Region 'Properties

#Region "Methods"

        Public Sub CancelAsync()
            If bwYT.IsBusy Then bwYT.CancelAsync()

            While bwYT.IsBusy
                Application.DoEvents()
                Threading.Thread.Sleep(50)
            End While
        End Sub

        Public Sub GetVideoLinks(ByVal url As String)
            Try
                _VideoLinks = ParseYTFormats(url, False)

            Catch ex As Exception
                Master.eLog.WriteToErrorLog(ex.Message, ex.StackTrace, "Error")
            End Try
        End Sub

        Public Sub GetVideoLinksAsync(ByVal url As String)
            Try
                If Not bwYT.IsBusy Then
                    _VideoLinks = Nothing
                    bwYT.WorkerSupportsCancellation = True
                    bwYT.RunWorkerAsync(url)
                End If
            Catch ex As Exception
                Master.eLog.WriteToErrorLog(ex.Message, ex.StackTrace, "Error")
            End Try
        End Sub

        Private Sub bwYT_DoWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles bwYT.DoWork
            Dim Url As String = DirectCast(e.Argument, String)

            Try
                e.Result = ParseYTFormats(Url, True)
            Catch ex As Exception
                Master.eLog.WriteToErrorLog(ex.Message, ex.StackTrace, "Error")
            End Try
        End Sub

        Private Sub bwYT_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bwYT.RunWorkerCompleted
            Try
                If e.Cancelled Then
                    'user cancelled
                    RaiseEvent VideoLinksRetrieved(False)
                ElseIf e.Error IsNot Nothing Then
                    'exception occurred
                    RaiseEvent Exception(e.Error)
                Else
                    'all good
                    If e.Result IsNot Nothing Then
                        _VideoLinks = DirectCast(e.Result, VideoLinkItemCollection)
                        RaiseEvent VideoLinksRetrieved(True)
                    Else
                        RaiseEvent VideoLinksRetrieved(False)
                    End If
                End If
            Catch ex As Exception
                Master.eLog.WriteToErrorLog(ex.Message, ex.StackTrace, "Error")
            End Try
        End Sub

        Private Function GetVideoTitle(ByVal HTML As String) As String
            Dim result As String = ""
            'Dim KeyPattern As String = "'VIDEO_TITLE':\s*'([^']*?)'"
            Dim KeyPattern As String = "meta name=\""title\"" content=\s*\""([^']*?)\"""
            'meta name="title" content=
            If Regex.IsMatch(HTML, KeyPattern) Then
                result = Regex.Match(HTML, KeyPattern).Groups(1).Value
            End If

            Return result
        End Function

        Private Function ParseYTFormats(ByVal url As String, ByVal doProgress As Boolean) As VideoLinkItemCollection
            Dim DownloadLinks As New VideoLinkItemCollection
            Dim sHTTP As New HTTP

            Try
                If bwYT.CancellationPending Then Return DownloadLinks

                Dim Html As String = sHTTP.DownloadData(url)
                If Html.ToLower.Contains("page not found") Then
                    Html = String.Empty
                End If

                If String.IsNullOrEmpty(Html.Trim) Then Return DownloadLinks
                If bwYT.CancellationPending Then Return DownloadLinks

                Dim VideoTitle As String = GetVideoTitle(Html)
                VideoTitle = Regex.Replace(VideoTitle, "['?\\:*<>]*", "")

                Dim fmtMatch As Match = Regex.Match(Html, "url_encoded_fmt_stream_map=(.*?)\\u0026amp;", RegexOptions.IgnoreCase)
                If fmtMatch.Success Then
                    Dim FormatMap As String = fmtMatch.Groups(1).Value

                    Dim Formats As String() = Web.HttpUtility.UrlDecode(FormatMap).Split(Convert.ToChar(","))
                    For Each fmt As String In Formats
                        Dim Splitter As String() = {"url=", "&itag="}
                        Dim FormatElements As String() = fmt.Split(Splitter, StringSplitOptions.RemoveEmptyEntries)

                        Dim Link As New VideoLinkItem

                        Select Case FormatElements(1).Trim
                            Case "18"
                                Link.Description = "SQ (MP4)"
                                Link.FormatQuality = Enums.TrailerQuality.SQMP4
                            Case "22"
                                Link.Description = "720p"
                                Link.FormatQuality = Enums.TrailerQuality.HD720p
                            Case "34"
                                Link.Description = "SQ (FLV)"
                                Link.FormatQuality = Enums.TrailerQuality.SQFLV
                            Case "35"
                                Link.Description = "HQ (FLV)"
                                Link.FormatQuality = Enums.TrailerQuality.HQFLV
                            Case "37"
                                Link.Description = "1080p"
                                Link.FormatQuality = Enums.TrailerQuality.HD1080p
                            Case "46"
                                Link.Description = "1080p (VP8)"
                                Link.FormatQuality = Enums.TrailerQuality.HD1080pVP8
                            Case "45"
                                Link.Description = "720p (VP8)"
                                Link.FormatQuality = Enums.TrailerQuality.HD720pVP8
                            Case "44"
                                Link.Description = "HQ (VP8)"
                                Link.FormatQuality = Enums.TrailerQuality.HQVP8
                            Case "43"
                                Link.Description = "SQ (VP8)"
                                Link.FormatQuality = Enums.TrailerQuality.SQVP8
                            Case Else
                                Link.Description = "Other"
                                Link.FormatQuality = Enums.TrailerQuality.OTHERS
                                'Continue For
                        End Select

                        Link.URL = Web.HttpUtility.UrlDecode(FormatElements(0)) & "&title=" & VideoTitle

                        If bwYT.CancellationPending Then Return DownloadLinks

                        If Not String.IsNullOrEmpty(Link.URL) AndAlso sHTTP.IsValidURL(Link.URL) Then
                            DownloadLinks.Add(Link)
                        End If

                        If bwYT.CancellationPending Then Return DownloadLinks

                    Next
                End If

                Return DownloadLinks

            Catch ex As Exception
                Master.eLog.WriteToErrorLog(ex.Message, ex.StackTrace, "Error")
                Return New VideoLinkItemCollection
            Finally
                sHTTP = Nothing
            End Try
        End Function

#End Region 'Methods

    End Class

    Public Class VideoLinkItem

#Region "Fields"

        Private _Description As String
        Private _FormatQuality As Enums.TrailerQuality
        Private _URL As String

#End Region 'Fields

#Region "Properties"

        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal value As String)
                _Description = value
            End Set
        End Property

        Public Property URL() As String
            Get
                Return _URL
            End Get
            Set(ByVal value As String)
                _URL = value
            End Set
        End Property

        Friend Property FormatQuality() As Enums.TrailerQuality
            Get
                Return _FormatQuality
            End Get
            Set(ByVal value As Enums.TrailerQuality)
                _FormatQuality = value
            End Set
        End Property

#End Region 'Properties

    End Class

    Public Class VideoLinkItemCollection
        Inherits Generic.SortedList(Of Enums.TrailerQuality, VideoLinkItem)

#Region "Methods"

        Public Shadows Sub Add(ByVal Link As VideoLinkItem)
            If Not MyBase.ContainsKey(Link.FormatQuality) Then MyBase.Add(Link.FormatQuality, Link)
        End Sub

#End Region 'Methods

    End Class

End Namespace

