## Table of Contents
1. [Security Policy](#security-policy)
2. [Security Overview](#security-overview)
3. [Supported Versions](#rupported-versions)
4. [Reporting a Vulnerability](#reporting-a-vulnerability)
5. [Account Security](#account-security)
6. [Microsoft Account Safety](#microsoft-account-safety)
7. [Offline Accounts](#offline-accounts)
8. [Plugin Sandboxing](#plugin-sandboxing)


# Security Policy
This is OrangLauncher Supported versions also bugfixes get released once a month and we publish this for one linux distro.
If you want to make this open to more distros, contact us first.

## Supported Versions
The Operating systems support, also we do not support python version of windows because issues and lag.

| Version             | Supported           |
| --------------------| ------------------- |
| Linux Global        | :white_check_mark:  |
| Fedora DNF          | :white_check_mark:  |
| Arch Linux Aur      | :white_check_mark:  |
| Windows 10 WPF WinUI| :white_check_mark:  |

## Reporting a Vulnerability

Go to issues tab and report here the vulnerability if you see any or pull request if you know how to code.

## Security Overview

OrangLauncher is designed with security as a core principle:

-  **Open Source**: All code available for audit
-  **No Telemetry**: Optional telemetry deletion of microslop
-  **Verified Downloads**: SHA-1 hashing for integrity
-  **Sandboxed Plugins**: Almost full access to launcher internals except some
-  **Regular Updates**: Security patches released quickly, sometimes

## Account Security

### Microsoft Account Safety

OrangLauncher uses Microsoft's official authentication:

1. **OAuth2 Flow**: Never handles passwords directly
3. **Token Refresh**: Automatic renewal before expiration
4. **Single Sign-On**: Integrated with Microsoft account

**Security Recommendations**:
- :white_check_mark: Enable 2-factor authentication on Microsoft account
- :white_check_mark: Use strong, unique passwords
- :white_check_mark: Regularly check account login history
- :white_check_mark: DO NOT PRESS LINKS ON PUBLIC SERVERS THAT YOU DON'T KNOW, INCLUDING GIVEAWAYS IF THEY ARE NOT FROM SERVER WEBSITE!!!
- :white_check_mark: Revoke access if launcher access compromised

### Offline Accounts

For offline play without authentication:

**Security Notes**:
- :white_check_mark: No account information required
- :white_check_mark: Purely local play
- :white_check_mark: Cannot join online servers (except LAN)
- :x: Not for multiplayer servers, but for "offline" ones like mc.oranges.lt

### Plugin Sandboxing

Plugins run in restricted environment with limited access:

**Plugins CAN:**
- Access profile/instance data
- Display UI elements
- Read game logs
- Create new profiles (with user interaction)

**Plugins CANNOT:**
- Execute arbitrary system commands
- Access other user files
- Modify launcher core functionality yet somewhat
- Access other profiles token data

**Last Updated**: April 2026  
**Security Level**: High (Open Source, Maintainable by others)  
**Report Issues**: GitHub Issues