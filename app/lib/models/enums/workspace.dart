enum WorkspaceType {
  Unspecified,
  Admin,
  Personal,
  Team,
}

WorkspaceType workspaceTypeFromString(String type) {
  if (type == null) {
    return WorkspaceType.Unspecified;
  }

  switch (type) {

    /// IMPORANT: for now we handle 'admin' workspaces as personal
    case "Admin":
      return WorkspaceType.Personal;
    case "Personal":
      return WorkspaceType.Personal;
    case "Team":
      return WorkspaceType.Team;
    default:
      return WorkspaceType.Unspecified;
  }
}

workspaceTypeToString(WorkspaceType type) {
  return type.toString().replaceAll('WorkspaceType.', '');
}

enum WorkspaceRole {
  Unknown,
  Admin,
  User,
}

WorkspaceRole workspaceRoleFromString(String type) {
  if (type == null) {
    return WorkspaceRole.Unknown;
  }

  switch (type) {
    case "Admin":
      return WorkspaceRole.Admin;
    case "User":
      return WorkspaceRole.User;
    default:
      return WorkspaceRole.Unknown;
  }
}

String workspaceRoleToString(WorkspaceRole type) {
  if (type == null) {
    return "";
  }

  switch (type) {
    case WorkspaceRole.Admin:
      return "Admin / Owner";
    case WorkspaceRole.User:
      return "User";
    default:
      return "Unknonw";
  }
}
