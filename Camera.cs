using OpenTK;

namespace Template
{
    public class Camera
    {
        public Vector3 Position { get; private set; }
        public Vector3 ViewDirection { get; private set; }
        public float FocalLength { get; private set; }

        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        
        public float AspectRatio { get; private set; }

        public Vector3 Right { get; private set; }
        public Vector3 Up { get; private set; }

        public Vector3 ImagePlaneCenter { get; private set; }
        
        public Vector3 TopLeft { get; private set; }
        public Vector3 TopRight { get; private set; }
        public Vector3 BottomLeft { get; private set; }
        
        public Vector3 pivot = Vector3.Zero; // pivot point of the camera

        public void SetPosition(Vector3 p)
        {
            Position = p;
            RecalculateParameters();
        }

        public void SetViewDirection(Vector3 v)
        {
            ViewDirection = v;
            RecalculateParameters();
        }

        public void SetFocalLength(float f)
        {
            FocalLength = f;
            RecalculateParameters();
        }

        public void RecalculateParameters()
        {
            AspectRatio = (float) ScreenWidth / ScreenHeight;
            Right = Vector3.Normalize(Vector3.Cross(ViewDirection, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, ViewDirection));
            ImagePlaneCenter = Position + FocalLength * ViewDirection;
            
            TopLeft = ImagePlaneCenter + Up - AspectRatio * Right;
            TopRight = ImagePlaneCenter + Up + AspectRatio * Right;
            BottomLeft = ImagePlaneCenter - Up - AspectRatio * Right;
        }

        public Camera(Vector3 position, Vector3 viewDirection, float focalLength, int screenWidth, int screenHeight)
        {
            this.Position = position;
            this.ViewDirection = viewDirection;
            this.FocalLength = focalLength;
            this.ScreenWidth = screenWidth;
            this.ScreenHeight = screenHeight;
            RecalculateParameters();
        }

        public Vector3 GetImagePlanePoint(int x, int y)
        {  
            return TopLeft 
                   + ((TopRight - TopLeft) * ((float)x / ScreenWidth))
                   + ((BottomLeft - TopLeft) * ((float)y / ScreenHeight));
        }

        public Ray CreateCameraRay(int x, int y)
        {
            var p = GetImagePlanePoint(x, y);
            return new Ray(Position, Vector3.Normalize(p - Position));
        }
    }
}