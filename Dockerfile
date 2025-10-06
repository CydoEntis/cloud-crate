\# --- Base runtime image ---
 FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
 WORKDIR /app
 EXPOSE 5000
 
 # --- Build stage ---
 FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
 WORKDIR /src
 COPY . .
 RUN dotnet publish CloudCrate.Api/CloudCrate.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
 
 # --- Final image ---
 FROM base AS final
 WORKDIR /app
 COPY --from=build /app/publish .
 
 # --- Environment variables ---
 ARG ConnectionStrings__DefaultConnection
 ARG Jwt__Key
 ARG Jwt__Issuer
 ARG Jwt__Audience
 ARG DefaultAdmin__Email
 ARG DefaultAdmin__Password
 ARG DefaultAdmin__DisplayName
 ARG Resend__ApiKey
 ARG Resend__FromEmail
 ARG Resend__FromName
 ARG Storage__Endpoint
 ARG Storage__AccessKey
 ARG Storage__SecretKey
 ARG Email__clientUrl
 ARG Frontend__BaseUrl
 
 ENV ConnectionStrings__DefaultConnection=$ConnectionStrings__DefaultConnection
 ENV Jwt__Key=$Jwt__Key
 ENV Jwt__Issuer=$Jwt__Issuer
 ENV Jwt__Audience=$Jwt__Audience
 ENV DefaultAdmin__Email=$DefaultAdmin__Email
 ENV DefaultAdmin__Password=$DefaultAdmin__Password
 ENV DefaultAdmin__DisplayName=$DefaultAdmin__DisplayName
 ENV Resend__ApiKey=$Resend__ApiKey
 ENV Resend__FromEmail=$Resend__FromEmail
 ENV Resend__FromName=$Resend__FromName
 ENV Storage__Endpoint=$Storage__Endpoint
 ENV Storage__AccessKey=$Storage__AccessKey
 ENV Storage__SecretKey=$Storage__SecretKey
 ENV Email__clientUrl=$Email__clientUrl
 ENV Frontend__BaseUrl=$Frontend__BaseUrl
 
 ENTRYPOINT ["dotnet", "CloudCrate.Api.dll"]
