param ([string]$pfxSecretStringValue,  [string]$pfxPswrd)
 
Write-Host("Certificate bytes size = $($pfxSecretStringValue.Length)")
Write-Host("Certificate password length = $($pfxPswrd.Length)")


# construct the certificate
$kvSecretBytes = [System.Convert]::FromBase64String($pfxSecretStringValue)
$certCollection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
$certCollection.Import($kvSecretBytes, $null, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)

# Get the file created
$protectedCertificateBytes = $certCollection.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12, $pfxPswrd)
$pfxPath ="./certificate.pfx"
[System.IO.File]::WriteAllBytes($pfxPath, $protectedCertificateBytes)

# write password file for use in nsis
[System.IO.File]::WriteAllText("./passwd.txt", $pfxPswrd)

# write file names for debugging
$colItems =  (get-childitem  "./" -include *.* -recurse | tee-object -variable files | measure-object -property length -sum)
$files | foreach-object {write-host $_.FullName}
