/*
 * Copyright (c) 2024 PlayEveryWare
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 */

namespace PlayEveryWare.EpicOnlineServices
{
    using Epic.OnlineServices;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains implementation of common functionality between different
    /// EOS Service managers (Currently AchievementsService and StatsService).
    /// </summary>
    public abstract class EOSService : IEOSSubManager, IDisposable
    {
        /// <summary>
        /// Describes the function signature for the event that triggers when
        /// the service has been updated.
        /// </summary>
        public delegate void ServiceUpdatedEventHandler();

        /// <summary>
        /// Event that triggers when changes have been made that may require a
        /// user interface update.
        /// </summary>
        public event ServiceUpdatedEventHandler Updated;

        /// <summary>
        /// Indicates whether the service needs to have a user authenticated in
        /// order to function properly.
        /// </summary>
        protected bool RequiresAuthentication { get; }

        /// <summary>
        /// Base constructor for Service managers.
        /// </summary>
        /// <param name="requiresAuthentication">
        /// Indicates whether the service manager requires a user to be
        /// authenticated with the Connect Interface in order to function
        /// properly.
        /// </param>
        protected EOSService(bool requiresAuthentication = true)
        {
            RequiresAuthentication = requiresAuthentication;
            AuthenticationListener.Instance.AuthenticationChanged += OnAuthenticationChanged;
            _ = RefreshAsync();
        }

        ~EOSService()
        {
            Dispose();
        }

        /// <summary>
        /// Try to get the product user id from the connect interface.
        /// </summary>
        /// <param name="productUserId">
        /// ProductUserId for the currently logged in user.
        /// </param>
        /// <returns>
        /// True if the ProductUserId was successfully retrieved and is valid,
        /// false otherwise.
        /// </returns>
        protected bool TryGetProductUserId(out ProductUserId productUserId)
        {
            productUserId = EOSManager.Instance.GetProductUserId();
            if (null != productUserId && productUserId.IsValid())
            {
                return true;
            }

            UnityEngine.Debug.LogWarning($"There was a problem getting the product user id of the currently logged-in user.");
            return false;

        }

        private void OnAuthenticationChanged(bool authenticated)
        {
            if (authenticated)
            {
                OnLoggedIn();
            }
            else
            {
                OnLoggedOut();
            }
        }

        /// <summary>
        /// Dispose function makes sure that the manager is removed from the
        /// collection of connect login listeners.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from the authentication changed event.
            AuthenticationListener.Instance.AuthenticationChanged -= OnAuthenticationChanged;
        }

        /// <summary>
        /// Implement this method to perform tasks when a user authenticates.
        /// </summary>
        protected abstract void OnLoggedIn();

        /// <summary>
        /// Implement this method to cleanup things when the user logs out. The
        /// local state information pertaining to the user should be cleared.
        /// </summary>
        protected abstract void OnLoggedOut();

        /// <summary>
        /// Refreshes the service manager
        /// </summary>
        /// <returns></returns>
        public async Task RefreshAsync()
        {
            // Check to see if authentication is required, if it's not then 
            // continue. If it is, then make sure a user is authenticated before
            // refreshing.
            if (!RequiresAuthentication || (RequiresAuthentication && AuthenticationListener.Instance.IsAuthenticated))
            {
                await InternalRefreshAsync();
            }
        }
        
        /// <summary>
        /// Implementation of this function should refresh the locally cached
        /// data managed by the service manager in question.
        /// </summary>
        protected abstract Task InternalRefreshAsync();

        /// <summary>
        /// Trigger the notification callbacks that have been registered,
        /// letting any consumers know that the data base been updated.
        /// </summary>
        protected void NotifyUpdated()
        {
            Updated?.Invoke();
        }
    }
}