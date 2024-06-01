#ifndef __LAB_COLORSPACE__
#define __LAB_COLORSPACE__

/*
 * Conversion between RGB and LAB colorspace.
 * Import from flowabs glsl program : https://code.google.com/p/flowabs/source/browse/glsl/?r=f36cbdcf7790a28d90f09e2cf89ec9a64911f138
 * Downloaded from https://gist.github.com/mattatz/44f081cac87e2f7c8980 at 2024-06-01
 */

float3 rgb2xyz( float3 c ) {
    float3 tmp;
    tmp.x = ( c.r > 0.04045 ) ? pow( ( c.r + 0.055 ) / 1.055, 2.4 ) : c.r / 12.92;
    tmp.y = ( c.g > 0.04045 ) ? pow( ( c.g + 0.055 ) / 1.055, 2.4 ) : c.g / 12.92,
    tmp.z = ( c.b > 0.04045 ) ? pow( ( c.b + 0.055 ) / 1.055, 2.4 ) : c.b / 12.92;
    const float3x3 mat = float3x3(
		0.4124, 0.3576, 0.1805,
        0.2126, 0.7152, 0.0722,
        0.0193, 0.1192, 0.9505 
	);
    return 100.0 * mul(tmp, mat);
}

float3 xyz2lab( float3 c ) {
    float3 n = c / float3(95.047, 100, 108.883);
    float3 v;
    v.x = ( n.x > 0.008856 ) ? pow( n.x, 1.0 / 3.0 ) : ( 7.787 * n.x ) + ( 16.0 / 116.0 );
    v.y = ( n.y > 0.008856 ) ? pow( n.y, 1.0 / 3.0 ) : ( 7.787 * n.y ) + ( 16.0 / 116.0 );
    v.z = ( n.z > 0.008856 ) ? pow( n.z, 1.0 / 3.0 ) : ( 7.787 * n.z ) + ( 16.0 / 116.0 );
    return float3(( 116.0 * v.y ) - 16.0, 500.0 * ( v.x - v.y ), 200.0 * ( v.y - v.z ));
}

float3 rgb2lab( float3 c ) {
    float3 lab = xyz2lab( rgb2xyz( c ) );
    return float3( lab.x / 100.0, 0.5 + 0.5 * ( lab.y / 127.0 ), 0.5 + 0.5 * ( lab.z / 127.0 ));
}

float3 lab2xyz( float3 c ) {
    float fy = ( c.x + 16.0 ) / 116.0;
    float fx = c.y / 500.0 + fy;
    float fz = fy - c.z / 200.0;
    return float3(
         95.047 * (( fx > 0.206897 ) ? fx * fx * fx : ( fx - 16.0 / 116.0 ) / 7.787),
        100.000 * (( fy > 0.206897 ) ? fy * fy * fy : ( fy - 16.0 / 116.0 ) / 7.787),
        108.883 * (( fz > 0.206897 ) ? fz * fz * fz : ( fz - 16.0 / 116.0 ) / 7.787)
    );
}

float3 xyz2rgb( float3 c ) {
	const float3x3 mat = float3x3(
        3.2406, -1.5372, -0.4986,
        -0.9689, 1.8758, 0.0415,
        0.0557, -0.2040, 1.0570
	);
    float3 v = mul(c / 100.0, mat);
    float3 r;
    r.x = ( v.r > 0.0031308 ) ? (( 1.055 * pow( v.r, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.r;
    r.y = ( v.g > 0.0031308 ) ? (( 1.055 * pow( v.g, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.g;
    r.z = ( v.b > 0.0031308 ) ? (( 1.055 * pow( v.b, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.b;
    return r;
}

float3 lab2rgb( float3 c ) {
    return xyz2rgb( lab2xyz( float3(100.0 * c.x, 2.0 * 127.0 * (c.y - 0.5), 2.0 * 127.0 * (c.z - 0.5)) ) );
}

// Personal additions
#define JND 1.0

// http://www.brucelindbloom.com/index.html?Eqn_DeltaE_CIE94.html
float deltaE94(float3 lab1, float3 lab2) {
    // Since we're doing graphic arts, use the following constants
    float kl = 1;
    float K1 = 0.045;
    float K2 = 0.015;

    // Preliminary variables
    float deltaL = lab1.x - lab2.x;
    float C1 = sqrt(lab1.y*lab1.y + lab1.z*lab1.z);
    float C2 = sqrt(lab2.y*lab2.y + lab2.z*lab2.z);
    float deltaC = C1 - C2;
    float deltaA = lab1.y - lab2.y;
    float deltaB = lab1.z - lab2.z;
    float deltaH = sqrt(deltaA*deltaA + deltaB*deltaB - deltaC*deltaC);
    float sl = 1;
    float sc = 1 + K1*C1;
    float sh = 1 + K2*C1;
    float kc = 1;
    float kh = 1;

    // Main formula parts
    float term1 = deltaL / (kl*sl);
    float term2 = deltaC / (kc*sc);
    float term3 = deltaH / (kh*sh);

    // Main formula
    float deltaE = sqrt( term1*term1 + term2*term2 + term3*term3 );
    return deltaE;
}

#endif