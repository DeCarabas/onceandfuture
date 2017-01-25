import React from 'react';
import { connect } from 'react-redux';
import {
  COLOR_VERY_LIGHT,

  SIZE_BANNER_HEIGHT,
  SIZE_SPACER_HEIGHT,

  Z_INDEX_ACCOUNT_SETTINGS,
} from './style';
import {
  accountSettingsToggle,
  setEmail,
  setPassword,
} from '../actions';
import {
  DISABLED,
  SettingInputBox,
  SettingPasswordBox,
  SettingsSectionTitle,
} from './settingscontrols';
import IconButton from './iconbutton';

const HeaderBox = ({onClose}) => {
  const style = {
    position: "relative",
    height: SIZE_BANNER_HEIGHT,
  };

  const h_style = {
    position: "absolute",
    top: 5,
    margin: 0,
  };

  const b_style = {
    position: "absolute",
    top: 0, right: 0,
  };

  return <div style={style}>
    <h2 style={h_style}>Account Settings</h2>
    <div style={b_style}>
      <IconButton tip="Close" icon="fa-window-close" onClick={onClose} />
    </div>
  </div>;
}

const ChangeEmailBox = ({email, emailState, setAddress}) => {
  let middlePart;
  if (emailState === 'PENDING') {
    const style = {
      textAlign: 'center',
      padding: SIZE_SPACER_HEIGHT,
    };
    middlePart = <div style={style}>(Fetching, please wait.)</div>;
  } else if (emailState === 'ERROR') {
    const style = {
      textAlign: 'center',
      padding: SIZE_SPACER_HEIGHT,
    };
    middlePart = <div style={style}>An unexpected error occurred. Please try again!</div>;
  } else {
    const validator = (value) => {
      // No message but not enabled.
      if (value === email) { return DISABLED; }
      if (emailState === 'SETTING') { return DISABLED; }

      if (!value || value.length == 0) {
        return 'Cannot have an empty address.';
      }

      return null;
    };

    const transientError = (emailState === 'SET_ERROR')
      ? 'An unexpected error occurred. Please try again.'
      : false;

    middlePart = <div>
      <p>Change your email address! Right here!</p>
      <SettingInputBox
        value={email}
        setValue={setAddress}
        buttonLabel='Change Email'
        validator={validator}
        transientError={transientError}
      />
    </div>;
  }

  return (
    <div>
      <SettingsSectionTitle text="Change Email Address" />
      {middlePart}
    </div>
  );
};

const ChangePasswordBox = ({passwordState, setPassword}) => {
  const validator = () => {
    if (passwordState === 'SETTING') { return DISABLED; }
    return null;
  };
  const transientError = (passwordState === 'SET_ERROR')
    ? 'An unexpected error occurred. Please try again.'
    : false;

  return (
    <div>
      <SettingsSectionTitle text="Change Password" />
      <p>Change your password by entering your new password below. Enter it
      twice, to make sure you can remember it later!</p>
      <SettingPasswordBox
        setValue={setPassword}
        buttonLabel='Change Password'
        validator={validator}
        transientError={transientError}
      />
    </div>
  );
};

const AccountSettingsBase = ({
  user,
  email,
  emailState,
  passwordState,
  onClose,
  onSetAddress,
  onSetPassword,
}) => {
  const style = {
    width: 460,
    marginLeft: 'auto',
    marginRight: 'auto',
    backgroundColor: COLOR_VERY_LIGHT,
    padding: 10,
    paddingBottom: 20,
    border: "2px solid black",
    borderRadius: 10,

    zIndex: Z_INDEX_ACCOUNT_SETTINGS,
  };

  const setAddress = (email) => onSetAddress(user, email);
  const setPassword = (password) => onSetPassword(user, password);
  return <div style={style}>
    <HeaderBox onClose={onClose} />
    <ChangeEmailBox email={email} emailState={emailState} setAddress={setAddress} />
    <ChangePasswordBox passwordState={passwordState} setPassword={setPassword} />
  </div>;
};


const mapStateToProps = (state) => {
  return {
    emailState: state.account_settings.emailState,
    email: state.account_settings.email,
    passwordState: state.account_settings.passwordState,
    user: state.user,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onClose: () => dispatch(accountSettingsToggle()),
    onSetAddress: (user, email) => dispatch(setEmail(user, email)),
    onSetPassword: (user, password) => dispatch(setPassword(user, password)),
  };
};

const AccountSettings = connect(mapStateToProps, mapDispatchToProps)(AccountSettingsBase);

export default AccountSettings;
