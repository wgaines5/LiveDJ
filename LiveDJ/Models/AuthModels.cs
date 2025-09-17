using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveDJ.Models;

public record FirebaseSignInRequest(string email, string password, bool returnSecureToken = true);
public record FirebaseSignUpRequest(string email, string password, bool returnSecureToken = true);


public record FirebaseSignInResponse(
string idToken,
string email,
string refreshToken,
string expiresIn,
string localId,
bool registered);


public record FirebaseRefreshRequest(string grant_type, string refresh_token);
public record FirebaseRefreshResponse(string access_token, string expires_in,
    string token_type, string refresh_token, string id_token, string user_id, string project_id);
