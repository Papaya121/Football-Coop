# Football WebGL deployment

Site: http://212.57.127.98/
Direct game: http://212.57.127.98/game/

Source build: `Builds/Web` (17 October 2026).
Published root: `/var/www/football`.
Nginx configuration: `/etc/nginx/conf.d/football.conf` (copy in this folder).

The main page embeds `/game/` in an iframe with fullscreen and autoplay permissions.
Brotli build files were decompressed for serving over plain HTTP; URLs in the game HTML and bridge script were changed accordingly. WebAssembly is served as `application/wasm`.

`site/` contains the deployed static files. Upload its contents into `/var/www/football`, then validate any Nginx changes with `nginx -t` before `systemctl reload nginx`.
Do not upload the Unity Burst debug directory. No server credentials are stored here.
