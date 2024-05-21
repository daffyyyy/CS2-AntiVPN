# CS2-AntiVPN

### Do you appreciate what I do? Buy me a cup of tea ❤️
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Y8Y4THKXG)

### Description
A plugin that kicks players using VPNs

### Requirments
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/) **tested on v233**

### Configuration
After first launch, u need to configure plugin in  addons/counterstrikesharp/configs/plugins/CS2-AntiVPN/CS2-AntiVPN.json
```json
{
"ApiKey": "", // Key from proxycheck.io (needed to detect country)
"DetectVpn": true, // Detect if user use vpn, u can disable for example when u need to check only country
"BlockedCountry": [], // Kick countries on list
"AllowedCountry": [], // Allow only countries on list, kick others
"AllowedIps": [], // Whitelist of ips
"PunishCommand": "css_ban #{userid} 18000 \"Cheats Detected #3\"", // Command to use when plugin detect vpn or country
"DatabaseHost": "", // MySQL database host
"DatabasePort": 3306, // MySQL database port
"DatabaseUser": "", // MySQL database username
"DatabasePassword": "", // MySQL database password
"DatabaseName": "", // MySQL database name
"ConfigVersion": 1
}
```

Where i can get ApiKey?
- https://proxycheck.io
