import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/workspace/blocs/create.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class WorkspaceCreatePage extends StatefulWidget {
  @override
  _WorkspaceCreatePageState createState() => _WorkspaceCreatePageState();
}

class _WorkspaceCreatePageState extends State<WorkspaceCreatePage> {
  final _bloc = WorkspaceCreateBloc();
  final TextEditingController _nameController = TextEditingController();
  var _pageController;

  final Map<String, String> _pageContent = {
    "title_text": "Create Team Name",
    "subtitle_text": "Business Pro",
    "helper_text":
        "Built for digital agencies and businesses needing advanced functionality.",
    "hint_text": "e.g.- Gravity Media",
    "link_existing":
        "Link existing Business Profiles - don't see the ones you want here... be sure to link them to your personal workspace first.",
  };

  @override
  void initState() {
    super.initState();

    _pageController = PageController();
    _pageController
        .addListener(() => _bloc.setPage(_pageController.page.toInt()));
    _nameController
        .addListener(() => _bloc.setName(_nameController.text.trim()));
  }

  @override
  void dispose() {
    _bloc.dispose();
    _pageController.dispose();
    _nameController.dispose();

    super.dispose();
  }

  /// kicks off process to create the workspace in the bloc
  /// then evaluate the result and either do nothing (on error)
  /// or redirect the user to the connect pages screen once done
  void _goToCreate() => _bloc.createWorkspace().then((success) {
        if (success) {
          _goToPages();
        }
      });

  void _goToPages() => Navigator.of(context).pushNamedAndRemoveUntil(
      AppRouting.getConnectPages, (Route<dynamic> route) => false);

  @override
  Widget build(BuildContext context) => StreamBuilder<WorkspaceCreateState>(
      stream: _bloc.page,
      builder: (context, snapshot) =>
          snapshot.data == WorkspaceCreateState.Creating
              ? _buildCreating()
              : snapshot.data == WorkspaceCreateState.Error
                  ? _buildError()
                  : _buildForm());

  Widget _buildForm() => Scaffold(
        appBar: AppBar(
          leading: AppBarCloseButton(context),
          elevation: 0,
          backgroundColor: Theme.of(context).scaffoldBackgroundColor,
          title: StreamBuilder<int>(
            stream: _bloc.onPage,
            builder: (context, snapshot) => AnimatedOpacity(
              duration: Duration(milliseconds: 350),
              opacity: snapshot.data == null || snapshot.data == 0 ? 0 : 1,
              child: Text("Choose Accounts"),
            ),
          ),
        ),
        body: SafeArea(
            bottom: true,
            child: PageView(
              controller: _pageController,
              children: <Widget>[
                _buildAccountName(_pageController),
                _buildPreview()
              ],
            )),
      );

  Widget _buildAccountName(PageController controller) => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(_pageContent["title_text"],
                    style: Theme.of(context).textTheme.headline6),
                Padding(
                  padding:
                      EdgeInsets.only(bottom: 8, left: 32, right: 32, top: 16),
                  child: TextFormField(
                    textInputAction: TextInputAction.next,
                    controller: _nameController,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodyText2.merge(
                          TextStyle(fontSize: 16.0, height: 1.0),
                        ),
                    maxLines: 1,
                    keyboardAppearance: Theme.of(context).brightness,
                    textCapitalization: TextCapitalization.sentences,
                    inputFormatters: [
                      LengthLimitingTextInputFormatter(25),
                    ],
                    decoration: InputDecoration(
                        alignLabelWithHint: false,
                        hintText: _pageContent["hint_text"],
                        border: UnderlineInputBorder()),
                  ),
                ),
                Container(
                  padding: EdgeInsets.only(top: 16, left: 32, right: 32),
                  child: StreamBuilder<bool>(
                      stream: _bloc.canChooseAccounts,
                      builder: (context, snapshot) => AnimatedOpacity(
                            duration: Duration(milliseconds: 350),
                            opacity: snapshot.data == true ? 1 : 0,
                            child: PrimaryButton(
                              label: "Continue",
                              onTap: snapshot.data == true
                                  ? () {
                                      FocusScope.of(context)
                                          .requestFocus(FocusNode());

                                      controller.animateToPage(1,
                                          duration: Duration(milliseconds: 350),
                                          curve: Curves.easeInOut);
                                    }
                                  : null,
                            ),
                          )),
                ),
              ],
            ),
          ),
          Padding(
            padding: EdgeInsets.only(bottom: 16, left: 32, right: 32, top: 8),
            child: Column(
              children: <Widget>[
                Stack(
                  alignment: Alignment.center,
                  children: <Widget>[
                    Container(
                      width: 55,
                      height: 55,
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            Theme.of(context).primaryColor,
                            AppColors.successGreen,
                            Colors.yellowAccent,
                          ],
                          stops: [0.1, 0.5, 0.9],
                          begin: Alignment.topRight,
                          end: Alignment.bottomLeft,
                        ),
                        borderRadius: BorderRadius.circular(80),
                      ),
                    ),
                    Container(
                      width: 51,
                      height: 51,
                      decoration: BoxDecoration(
                        color: Theme.of(context).appBarTheme.color,
                        borderRadius: BorderRadius.circular(80),
                      ),
                    ),
                    Container(
                      height: 32.0,
                      width: 32.0,
                      decoration: BoxDecoration(
                        image: DecorationImage(
                          image: AssetImage("assets/icons/pro-icon.png"),
                        ),
                      ),
                    ),
                  ],
                ),
                Padding(
                  padding: EdgeInsets.only(bottom: 4, top: 12),
                  child: Text(
                    _pageContent["subtitle_text"],
                    style: Theme.of(context).textTheme.bodyText1.merge(
                          TextStyle(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                  ),
                ),
                Text(
                  _pageContent["helper_text"],
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ],
            ),
          ),
        ],
      );

  Widget _buildPreview() => Column(
        children: <Widget>[
          Expanded(child: Text("Tap 'Create Team' to continue")),
          Container(
            padding: EdgeInsets.all(16),
            child: StreamBuilder<bool>(
              stream: _bloc.canPreview,
              builder: (context, snapshot) => snapshot.data == false
                  ? Container()
                  : PrimaryButton(
                      buttonColor: Theme.of(context).primaryColor,
                      labelColor: Theme.of(context).scaffoldBackgroundColor,
                      label: "Create Team",
                      onTap: _goToCreate,
                    ),
            ),
          ),
        ],
      );

  Widget _buildCreating() => Scaffold(
        body: SafeArea(
          bottom: true,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Container(
                width: double.infinity,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Stack(
                      alignment: Alignment.center,
                      children: <Widget>[
                        Container(
                          width: 88,
                          height: 88,
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              colors: [
                                Theme.of(context).primaryColor,
                                AppColors.successGreen,
                                Colors.yellowAccent,
                              ],
                              stops: [0.1, 0.5, 0.9],
                              begin: Alignment.topRight,
                              end: Alignment.bottomLeft,
                            ),
                            borderRadius: BorderRadius.circular(80),
                          ),
                        ),
                        Container(
                          width: 83,
                          height: 83,
                          decoration: BoxDecoration(
                            color: Theme.of(context).scaffoldBackgroundColor,
                            borderRadius: BorderRadius.circular(80),
                          ),
                        ),
                        Container(
                          height: 52.0,
                          width: 52.0,
                          decoration: BoxDecoration(
                            image: DecorationImage(
                              image: AssetImage("assets/icons/pro-icon.png"),
                            ),
                          ),
                        ),
                        Container(
                          width: 85,
                          height: 85,
                          child: CircularProgressIndicator(
                            backgroundColor: Colors.transparent,
                            strokeWidth: 5,
                            valueColor: AlwaysStoppedAnimation<Color>(
                              Theme.of(context).scaffoldBackgroundColor,
                            ),
                          ),
                        )
                      ],
                    ),
                    StreamBuilder<int>(
                      stream: _bloc.creatingStep,
                      builder: (context, snapshot) {
                        final int currentStep = snapshot.data ?? 0;

                        return Padding(
                          padding: EdgeInsets.only(top: 16),
                          child: AnimatedCrossFade(
                            crossFadeState: currentStep == 0
                                ? CrossFadeState.showFirst
                                : CrossFadeState.showSecond,
                            duration: Duration(milliseconds: 350),
                            firstChild: Text("Creating ${_bloc.name}",
                                style: Theme.of(context).textTheme.bodyText1),
                            secondChild: AnimatedCrossFade(
                              crossFadeState: currentStep != 2
                                  ? CrossFadeState.showFirst
                                  : CrossFadeState.showSecond,
                              duration: Duration(milliseconds: 350),
                              firstChild: Text("Adding accounts...",
                                  style: Theme.of(context).textTheme.bodyText1),
                              secondChild: Text("Finalizing...",
                                  style: Theme.of(context).textTheme.bodyText1),
                            ),
                          ),
                        );
                      },
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      );

  Widget _buildError() => Scaffold(
        appBar: AppBar(
          leading: AppBarCloseButton(
            context,
            onPressed: _goToPages,
          ),
          title: Text("Something went wrong..."),
        ),
        body: SafeArea(
          bottom: true,
          child: Container(
            padding: EdgeInsets.all(16),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text("We were unable to complete this action..."),
                SizedBox(height: 32),
                SecondaryButton(
                  fullWidth: true,
                  label: "Start over",
                  onTap: _goToPages,
                ),
              ],
            ),
          ),
        ),
      );
}
