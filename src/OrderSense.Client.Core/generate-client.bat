@echo off
echo Оновлення клієнта OpenAPI...
dotnet tool restore
dotnet nswag run nswag.json
echo Клієнт успішно згенеровано!
pause