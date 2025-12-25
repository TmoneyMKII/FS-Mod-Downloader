# Security Policy

## Supported Versions

The following versions of FS Mod Downloader are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

As a newly released project, only the latest version receives security updates. We recommend always using the most recent release.

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability in FS Mod Downloader, please report it responsibly.

### How to Report

**Please DO NOT open a public GitHub issue for security vulnerabilities.**

Instead, report vulnerabilities by:

1. **Email:** Send details to the repository owner via GitHub profile contact
2. **Private Disclosure:** Use [GitHub's private vulnerability reporting](https://github.com/TmoneyMKII/FS-Mod-Downloader/security/advisories/new)

### What to Include

- Description of the vulnerability
- Steps to reproduce the issue
- Potential impact assessment
- Any suggested fixes (optional but appreciated)

### Response Timeline

- **Initial Response:** Within 72 hours
- **Status Update:** Within 7 days
- **Resolution Target:** Within 30 days for critical issues

### What to Expect

- We will acknowledge receipt of your report
- We will investigate and validate the vulnerability
- We will work on a fix and coordinate disclosure timing with you
- We will credit you in the release notes (unless you prefer anonymity)

## Security Considerations

### Mod Downloads

- Mods are downloaded from third-party websites (mod-network.com, etc.)
- We do not host or verify mod files
- Always use reputable mod sources
- Your antivirus should scan downloaded files

### Network Traffic

- The app makes HTTP/HTTPS requests to mod hosting websites
- No personal data is collected or transmitted
- No telemetry or analytics are included

### Local Storage

- Settings stored in `%AppData%\FSModDownloader\`
- Log files may contain file paths and URLs
- No passwords or sensitive credentials are stored

### Permissions

- The app requires write access to your game's mods folder
- No administrator privileges required for normal operation
- No system-level modifications are made

## Third-Party Dependencies

We use the following dependencies, which have their own security considerations:

| Package | Purpose |
|---------|---------|
| HtmlAgilityPack | HTML parsing (no code execution) |
| Serilog | Logging (local only) |
| CommunityToolkit.Mvvm | UI framework |
| RestSharp | HTTP requests |

Dependencies are kept up-to-date via Dependabot.
