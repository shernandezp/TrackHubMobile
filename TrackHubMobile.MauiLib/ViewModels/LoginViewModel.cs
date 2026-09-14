// Copyright (c) 2025 Sergio Hernandez. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Utils;

namespace TrackHubMobile.ViewModels;

public partial class LoginViewModel(IAuthentication authentication, IStorage storage) : BaseViewModel
{
    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe;

    [ObservableProperty]
    private bool showPassword;

    [ObservableProperty]
    private bool busy;

    [ObservableProperty]
    private string? error;

    public Action? OnUpdated { get; set; }

    public bool CanSubmit => !Busy && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrEmpty(Password);

    public async Task LoadAsync()
    {
        var rememberedEmail = await storage.GetSecure(Constants.RememberedEmail);
        if (string.IsNullOrEmpty(rememberedEmail))
        {
            return;
        }

        RememberMe = true;
        Email = rememberedEmail;
        Password = await storage.GetSecure(Constants.RememberedPassword) ?? string.Empty;
    }

    public void TogglePasswordVisibility() => ShowPassword = !ShowPassword;

    public async Task<bool> SignInAsync()
    {
        if (!CanSubmit)
        {
            return false;
        }

        Busy = true;
        Error = null;
        OnUpdated?.Invoke();
        try
        {
            var email = Email.Trim();
            Error = await authentication.SignInAsync(email, Password);
            if (Error is null)
            {
                await RememberCredentialsAsync(email);
                Password = string.Empty;
                ShowPassword = false;
                return true;
            }

            return false;
        }
        finally
        {
            Busy = false;
            OnUpdated?.Invoke();
        }
    }

    private async Task RememberCredentialsAsync(string email)
    {
        if (RememberMe)
        {
            await storage.SetSecure(Constants.RememberedEmail, email);
            await storage.SetSecure(Constants.RememberedPassword, Password);
        }
        else
        {
            storage.ClearSecure(Constants.RememberedEmail);
            storage.ClearSecure(Constants.RememberedPassword);
        }
    }
}
