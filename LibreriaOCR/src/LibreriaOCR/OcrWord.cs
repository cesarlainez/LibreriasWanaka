namespace LibreriaOCR;

/// <summary>Una palabra reconocida por OCR, con su caja (en píxeles de la imagen) y su confianza (0–100).</summary>
/// <param name="Text">Texto de la palabra.</param>
/// <param name="Confidence">Confianza en porcentaje (0–100).</param>
/// <param name="X">Coordenada X de la esquina superior izquierda.</param>
/// <param name="Y">Coordenada Y de la esquina superior izquierda.</param>
/// <param name="Width">Ancho de la caja.</param>
/// <param name="Height">Alto de la caja.</param>
public readonly record struct OcrWord(string Text, float Confidence, int X, int Y, int Width, int Height);
