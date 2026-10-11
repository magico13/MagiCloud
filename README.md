# MagiCloud
Personal "cloud" file storage backed by elasticsearch

## Goggles image: no local OCR

The `goggles` Docker image (built from `Goggles.dockerfile`) does **not** ship
Tesseract OCR. Image uploads are still accepted, but no text is extracted from
them unless **Azure OCR** is configured (see
`Goggles/ServiceCollectionExtensions.cs`). To re-enable local Tesseract OCR,
add the native `libtesseract`/`libleptonica` libraries and their soname
symlinks back into the image.
