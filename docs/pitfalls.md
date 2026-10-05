# Pitfalls

Things that went wrong in Relay and took longer to find than to fix. Newest first. Pitfalls that can
hit other repositories are also in CodePrint's `docs/pitfalls/`.

## The phone can't reach the agent because the firewall blocks the Private profile

- Symptom: the phone stays on "Connecting..." or "The PC didn't answer", while the agent log shows
  no connection at all. From the phone, `adb shell toybox nc -w 3 <pc-ip> 8731` times out, but the
  agent listens (`Get-NetTCPConnection -LocalPort 8731 -State Listen`).
- Cause: the PC's network is a Private network, and Windows Firewall has no Allow rule for
  `Relay.Agent.exe` on the Private profile. On first launch someone answered the firewall prompt
  for Public only, which left a Block rule for Private. Deleting that Block rule is not enough:
  Windows asks only when no rule for the program exists, and the Public Allow rules still exist, so
  the Private default (block inbound) applies and nothing prompts again.
- Fix: check `Get-NetFirewallApplicationFilter | ? Program -like '*relay*' | Get-NetFirewallRule`
  against `Get-NetConnectionProfile`. Then, from an admin PowerShell, allow the installed exe on
  Private and Public: TCP 8731 (the WebSocket) and UDP 5353 (mDNS discovery).
- Closed off by: not yet. A board idea has the agent detect this and offer a fix through UAC. The
  phone's timeout message now names the firewall.
- Seen: 2026-10-05, after the agent 0.10.0 and app 0.6.0 install (lukr-99/Relay#2).

## WPF UI's implicit styles restyle the inside of the agent's own templates

- Symptom: after moving onto `DotNetLib.Tray`, the agent's combo boxes looked cramped and its small
  number boxes drew stray lines, although its own implicit Button, TextBox and ComboBox styles still
  applied.
- Cause: `TrayResources.Merge` adds WPF UI's ControlsDictionary to the application. Merging the
  agent's dictionary after it keeps the agent's styles for those types, but the controls *inside*
  its templates (the ScrollViewer in a TextBox's PART_ContentHost, the ToggleButton in a ComboBox
  template) still pick up WPF UI's implicit styles.
- Fix: empty implicit styles in `Themes/Agent.xaml` put the plain WPF look back:
  `<Style TargetType="ScrollViewer"/>`, `ToggleButton`, `RepeatButton` and `Thumb`.
- Closed off by: not yet (screenshots compared by hand against 0.9.0).
- Seen: 2026-10-05, lukr-99/Relay#1.
