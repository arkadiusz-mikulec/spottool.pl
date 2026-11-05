Co zmieniamy
1. App.razor z <HeadOutlet @rendermode="InteractiveServer" />  na  <HeadOutlet @rendermode="new InteractiveServerRenderMode(prerender: false)" /> 
nie potrzebujemy prerenderingu
patrz: f/bad2_Intractive.... branch readme.me file



informacje nt. trybów renderowania
ponizsze stosujemy na stronach
```
@attribute [RenderModeInteractiveServer(prerender: false)]
```
mozna zribc tak ze zostawai sie prerendering w projekcie `<HeadOutlet @rendermode="InteractiveServer" />`
a na konkretnych stronach sie go wyłącza.

https://www.google.com/search?sourceid=chrome&ie=UTF-8&amc=1&gs_lcrp=EgZjaHJvbWUyBggAEEUYOdIBCzEyMjkzMWowajMxqAIGsAIB6gcKCAEQABgAIAAoAA&cud=0&atvm=2&sxsrf=AKL5yDhAuw5qEbCUJliOIbbkJgTy3-oD5Q%3A1793891900964&ved=0CCsQ2_wOahcKEwiQz4SOkPGXAxUAAAAAHQAAAAAQVA&mstk=AUtExfDG8btENVuOftiATVWOw1lHynxEMis-WvpEY787td7cEyy-3dVbZatXPYlXbUF6CPXQQbim1Q0hKGlaUj3NI6e4M1O5og5wqcMTKOlBmORNnT2AW643r8QFFGH3S8gsRhh7VckO6z6NAzVsUBJfKTIqpMYpvWyriIW1rogN49dx9hHC8GqwUGntThSno1Lu6RBFytIu5aHS_56ZRad8j2gLUmWkijY36vht3WapQ1VgqqTf1DD4bjYrOUY-_ZhTM0SvTsF8GX5xAg&mtid=fLPlarLXEZOrwPAPgLyjqQc&csuir=1&aep=26&q=blazor+serwer+app.+potrzebuje+przechowac+dane+uzytkownika+lokalnie+lepiej+uzyc+lokal+storage+czy+cookie&udm=50