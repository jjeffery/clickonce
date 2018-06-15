Release executable signed using the following command.

```
signtool sign /a /fd sha256 /n "Software Projects" /tr http://timestamp.globalsign.com/?signature=sha2 /td sha256 /d ClickOnce.exe ClickOnce.exe
```