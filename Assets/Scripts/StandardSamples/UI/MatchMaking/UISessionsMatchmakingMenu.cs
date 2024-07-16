/*
* Copyright (c) 2021 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;

using PlayEveryWare.EpicOnlineServices;

namespace PlayEveryWare.EpicOnlineServices.Samples
{
    public class UISessionsMatchmakingMenu : MonoBehaviour, ISampleSceneUI
    {
        public GameObject SessionsMatchmakingUIParent;

        [Header("Sessions/Matchmaking UI - Create Options")]
        public Text SessionNameVal;
        public Dropdown MaxPlayersVal;
        public Dropdown LevelVal;
        public Dropdown PermissionVal;
        public Toggle PresenceVal;
        public Toggle JoinInProgressVal;
        public Toggle InvitesAllowedVal;
        public Toggle SanctionsVal;

        [Header("Sessions/Matchmaking UI - Session Members")]
        public GameObject UISessionEntryPrefab;
        public GameObject SessionContentParent;
        public Text CurrentSessionsHeader;

        [Header("Sessions/Matchmaking UI - Search")]
        public UIConsoleInputField SearchByLevelBox;

        private bool ShowSearchResults = false;

        [Header("Sessions/Matchmaking UI - Invite PopUp")]
        public GameObject UIInvitePanel;
        public Text InviteFromVal;
        public Toggle InvitePresence;

        [Header("Controller")]
        public GameObject UIFirstSelected;

        private EOSSessionsManager GetEOSSessionsManager
        {
            get { return EOSManager.Instance.GetOrCreateManager<EOSSessionsManager>(); }
        }

        public void Awake()
        {
            // Hide Invite Pop-up (Default)
            UIInvitePanel.SetActive(false);
            InviteFromVal.text = string.Empty;

            HideMenu();

            GetEOSSessionsManager.UIOnSessionRefresh = OnSessionRefresh;
        }

        /*private void Start()
        {
        }*/

        private int previousFrameSessionCount = 0;
        private int previousFrameResultCount = 0;

        private void OnDestroy()
        {
            //HideMenu();
            // Unity crashes if you try to access EOSSinglton OnDestroy
            EOSManager.Instance.RemoveManager<EOSSessionsManager>();
        }

        public void Update()
        {
            EOSSessionsManager sessionsManager = GetEOSSessionsManager;
            bool stateUpdates = sessionsManager.Update();


            // Invites UI Prompt
            if (sessionsManager.GetCurrentInvite() != null)
            {
                UIInvitePanel.SetActive(true);

                if (string.IsNullOrEmpty(InviteFromVal.text))
                {
                    SessionAttribute attributeFound = sessionsManager.GetCurrentInvite().Attributes.Find(x => string.Equals(x.Key, "Level", StringComparison.OrdinalIgnoreCase));

                    if (attributeFound != null)
                    {
                        InviteFromVal.text = attributeFound.AsString;
                    }
                }
            }
            else
            {
                UIInvitePanel.SetActive(false);
                InviteFromVal.text = string.Empty;
            }

            if (ShowSearchResults)
            {
                previousFrameSessionCount = 0;

                if (sessionsManager.GetCurrentSearch() == null)
                {
                    Debug.LogError("Sessions Matchmaking (Update): ShowSearchResults is true, but CurrentSearch is null!");
                    ShowSearchResults = false;
                }

                CurrentSessionsHeader.text = "Search Results:";

                Dictionary<Session, SessionDetails> results = sessionsManager.GetCurrentSearch().GetResults();

                if (previousFrameResultCount == results.Count)
                {
                    if (results.Count == 0)
                    {
                        // Destroy current UI member list
                        foreach (Transform child in SessionContentParent.transform)
                        {
                            Destroy(child.gameObject);
                        }
                    }

                    // no new results count
                    return;
                }

                // Render Sessions State changes
                previousFrameResultCount = results.Count;

                foreach (KeyValuePair<Session, SessionDetails> kvp in sessionsManager.GetCurrentSearch().GetResults())
                {
                    Session sessionResult = kvp.Key;

                    GameObject sessionUiObj = Instantiate(UISessionEntryPrefab, SessionContentParent.transform);
                    UISessionEntry uiEntry = sessionUiObj.GetComponent<UISessionEntry>();

                    if (uiEntry != null)
                    {
                        uiEntry.SetUIElementsFromSessionAndDetails(sessionResult, kvp.Value, this);
                    }
                }
            }
            else
            {
                previousFrameResultCount = 0;

                CurrentSessionsHeader.text = "Current Sessions:";

                if (!stateUpdates && previousFrameSessionCount == sessionsManager.GetCurrentSessions().Count)
                {
                    if (sessionsManager.GetCurrentSessions().Count == 0)
                    {
                        // Destroy current UI member list
                        foreach (Transform child in SessionContentParent.transform)
                        {
                            GameObject.Destroy(child.gameObject);
                        }
                    }

                    // no state updates and count hasn't changed;
                    return;
                }

                // Render Sessions State changes
                previousFrameSessionCount = sessionsManager.GetCurrentSessions().Count;

                // Destroy current UI member list
                foreach (Transform child in SessionContentParent.transform)
                {
                    GameObject.Destroy(child.gameObject);
                }

                // Enumerate session entries in UI
                foreach (KeyValuePair<string, Session> kvp in sessionsManager.GetCurrentSessions())
                {
                    Session session = kvp.Value;

                    GameObject sessionUiObj = Instantiate(UISessionEntryPrefab, SessionContentParent.transform);
                    UISessionEntry uiEntry = sessionUiObj.GetComponent<UISessionEntry>();

                    if (uiEntry != null)
                    {
                        uiEntry.SetUIElementsFromSession(session, this);
                    }
                }
            }
        }

        public void CreateNewSessionOnClick()
        {
            Session session = new Session();
            session.AllowJoinInProgress = JoinInProgressVal.isOn;
            session.InvitesAllowed = InvitesAllowedVal.isOn;
            session.SanctionsEnabled = SanctionsVal.isOn;
            session.MaxPlayers = (uint)Int32.Parse(MaxPlayersVal.options[MaxPlayersVal.value].text);
            session.Name = SessionNameVal.text;
            session.PermissionLevel = (OnlineSessionPermissionLevel)PermissionVal.value;

            SessionAttribute attribute = new SessionAttribute();
            attribute.Key = "Level";
            attribute.AsString = LevelVal.options[LevelVal.value].text;
            attribute.ValueType = AttributeType.String;
            attribute.Advertisement = SessionAttributeAdvertisementType.Advertise;

            session.Attributes.Add(attribute);

            GetEOSSessionsManager.CreateSession(session, PresenceVal.isOn, UIOnSessionCreated);
        }

        private void UIOnSessionCreated()
        {
            // Update() already enumerates ActiveSessions.  Here you can do any UI related calls after session is created.
        }

        //Search Result
        public void JoinButtonOnClick(SessionDetails sessionHandle)
        {
            GetEOSSessionsManager.JoinSession(sessionHandle, true, OnJoinSessionFinished); // Default Presence True
        }

        private void OnJoinSessionFinished(Result result)
        {
            if (result != Result.Success)
            {
                RefreshSearch();
            }
            else
            {
                ShowSearchResults = false;
            }
        }

        // Session Member
        public void StartButtonOnClick(string sessionName)
        {
            GetEOSSessionsManager.StartSession(sessionName);
        }

        public void EndButtonOnClick(string sessionName)
        {
            GetEOSSessionsManager.EndSession(sessionName);
        }

        public void ModifyButtonOnClick(string sessionName)
        {
            // Only modify Max Players and Level
            Session session = new Session(); //GetEOSSessionsManager.GetSession(sessionName);
            session.Name = sessionName;
            session.MaxPlayers = (uint)Int32.Parse(MaxPlayersVal.options[MaxPlayersVal.value].text);
            session.AllowJoinInProgress = JoinInProgressVal.isOn;
            session.InvitesAllowed = InvitesAllowedVal.isOn;
            session.PermissionLevel = (OnlineSessionPermissionLevel)PermissionVal.value;

            SessionAttribute attr = new SessionAttribute();
            attr.Key = "Level";
            attr.ValueType = AttributeType.String;
            attr.AsString = LevelVal.options[LevelVal.value].text;
            attr.Advertisement = SessionAttributeAdvertisementType.Advertise;
            session.Attributes.Add(attr);

            GetEOSSessionsManager.ModifySession(session, OnModifySessionCompleted);
        }

        private void OnModifySessionCompleted()
        {
            previousFrameSessionCount = 0;
        }

        public void LeaveButtonOnClick(string sessionName)
        {
            GetEOSSessionsManager.DestroySession(sessionName);
        }

        public void RefreshSearch()
        {
            SearchByLevelEndEdit(SearchByLevelBox.InputField.text);
        }

        // Search
        public void SearchByLevelEndEdit(string searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
            {
                ShowSearchResults = false;
                return;
            }

            SessionAttribute levelAttribute = new SessionAttribute();
            levelAttribute.Key = "Level";
            levelAttribute.ValueType = AttributeType.String;
            levelAttribute.AsString = searchPattern.ToUpper();
            levelAttribute.Advertisement = SessionAttributeAdvertisementType.Advertise;

            List<SessionAttribute> attributes = new List<SessionAttribute>() { levelAttribute };

            GetEOSSessionsManager.Search(attributes);

            previousFrameResultCount = 0;
            ShowSearchResults = true;
        }

        // Invite
        public void AcceptInviteButtonOnClick()
        {
            bool invitePresenceToggled = InvitePresence.isOn;

            GetEOSSessionsManager.AcceptLobbyInvite(invitePresenceToggled);

            // Make sure UI is showing current sessions
            ShowSearchResults = false;
        }

        public void DeclineInviteButtonOnClick()
        {
            GetEOSSessionsManager.DeclineLobbyInvite();
        }

        public void ShowMenu()
        {
            GetEOSSessionsManager.OnLoggedIn();

            SessionsMatchmakingUIParent.gameObject.SetActive(true);

            // Controller
            EventSystem.current.SetSelectedGameObject(UIFirstSelected);

            EOSManager.Instance.SetLogLevel(Epic.OnlineServices.Logging.LogCategory.AllCategories, Epic.OnlineServices.Logging.LogLevel.Warning);
            EOSManager.Instance.SetLogLevel(Epic.OnlineServices.Logging.LogCategory.Sessions, Epic.OnlineServices.Logging.LogLevel.Verbose);
        }

        public void HideMenu()
        {
            if (GetEOSSessionsManager.IsUserLoggedIn)//check to prevent warnings when done unnecessarily during Sessions & Matchmaking startup
            {
                GetEOSSessionsManager.OnLoggedOut();
            }

            SessionsMatchmakingUIParent.gameObject.SetActive(false);
        }

        public bool TryGetExistingUISessionEntryById(string sessionId, out UISessionEntry entry)
        {
            foreach (Transform childTransform in SessionContentParent.transform)
            {
                UISessionEntry thisEntry = childTransform.GetComponent<UISessionEntry>();

                if (null == thisEntry || null == thisEntry.RepresentedSession || thisEntry.RepresentedSession.Id != sessionId)
                {
                    continue;
                }

                entry = thisEntry;
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// After a Session is successfully refreshed, this is run to update the UI with the new information about the local Session.
        /// </summary>
        /// <param name="session">Information about the Session from the EOS C# SDK.</param>
        /// <param name="details">Additional information about the Session from the EOS C SDK.</param>
        public void OnSessionRefresh(Session session, SessionDetails details)
        {
            if (!TryGetExistingUISessionEntryById(session.Id, out UISessionEntry uiEntry))
            {
                Debug.Log($"UISessionsMatchmakingMenu (OnSessionRefresh): Requested refresh of a Session with {nameof(Session.Id)} \"{session.Id}\", but did not have a UI Entry for that currently. Cannot refresh it.");
                return;
            }

            Debug.Log($"{nameof(UISessionsMatchmakingMenu)} ({nameof(OnSessionRefresh)} Instructed to refresh Session with {nameof(Session.Id)} \"{session.Id}\". Found local UI element. Refreshing now.");

            uiEntry.SetUIElementsFromSessionAndDetails(session, details, this);
        }
    }
}