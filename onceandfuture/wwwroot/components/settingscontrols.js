import React from 'react';
import {
  COLOR_DARK,
  COLOR_LIGHT_GREY,
  COLOR_VERY_DARK,
  COLOR_VERY_LIGHT_GREY,

  SIZE_SPACER_HEIGHT,
} from './style';


export const SettingsSectionTitle = ({text}) => {
  const style = {
    fontWeight: 'bold',
  };

  return <div style={style}>{text}</div>;
}

export const SettingsButton = ({onClick, text, error, enabled=true}) => {
  const div_style = {
    textAlign: 'right',
    marginTop: SIZE_SPACER_HEIGHT,
  };
  const validation_error_style = Object.assign({
    color: 'red',
    display: 'inline-block',
    paddingRight: 5,
  });

  const base_style = {
    display: 'inline-block',
    padding: 3,
  };

  let style, handler;
  if (enabled) {
    style = Object.assign({}, base_style, {
      cursor: 'pointer',
      color: 'white',
      backgroundColor: COLOR_DARK,
      border: '2px solid ' + COLOR_VERY_DARK,
    });
    handler = onClick;
  } else {
    style = Object.assign({}, base_style, {
      cursor: 'default',
      color: COLOR_VERY_LIGHT_GREY,
      backgroundColor: COLOR_LIGHT_GREY,
      border: '2px solid ' + COLOR_LIGHT_GREY,
    });
    handler = null;
  }

  return (
    <div style={div_style}>
      <div style={validation_error_style}>
        <span>{error}</span>
      </div>
      <div style={style} onClick={handler}>{text}</div>
    </div>
  );
};

export const DISABLED = '';

export class SettingInputBox extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      value: props.value || '',
      transient_invalid: props.transientError,
    };

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }

  componentWillReceiveProps(props) {
    this.setState({transient_invalid: props.transientError});
  }

  handleChange(event) {
    this.setState({value: event.target.value, transient_invalid: false});
  }

  handleSubmit(event) {
    if (this.isValid()) {
      this.props.setValue(this.state.value);
      event.preventDefault();
    }
  }

  invalidReason() {
    if (this.props.validator) {
      return this.props.validator(this.state.value);
    } else {
      return null;
    }
  }

  isValid() {
    return this.invalidReason() === null;
  }

  render() {
    const input_style = {
      width: '100%',
    };

    const errorText = this.state.transient_invalid || this.invalidReason();
    return <div>
      <input
        style={input_style}
        type={this.props.kind}
        value={this.state.value}
        onChange={this.handleChange}
      />
      <SettingsButton
        onClick={this.handleSubmit}
        text={this.props.buttonLabel}
        error={errorText}
        enabled={this.isValid()}
      />
   </div>;
  }
}

export class SettingPasswordBox extends SettingInputBox {
  constructor(props) {
    super(props);
    this.state = Object.assign({}, this.state, {
      value: '',
      value_second: '',
    });

    this.handleChangeSecond = this.handleChangeSecond.bind(this);
  }

  handleChangeSecond(event) {
    this.setState({value_second: event.target.value, transient_invalid: false});
  }

  invalidReason() {
    if (this.state.value == '') {
      return '';
    } else if (this.state.value !== this.state.value_second) {
      return "The two passwords don't match.";
    } else {
      return super.invalidReason();
    }
  }

  render() {
    const input_style = {
      width: '100%',
    };

    const input_second = Object.assign({}, input_style, {
      marginTop: SIZE_SPACER_HEIGHT,
    });

    const errorText = this.state.transient_invalid || this.invalidReason();
    return <div>
      <input
        style={input_style}
        type="password"
        placeholder="New Password Here!"
        value={this.state.value}
        onChange={this.handleChange}
      />
      <input
        style={input_second}
        type="password"
        placeholder="New Password Again, Please!"
        value={this.state.value_second}
        onChange={this.handleChangeSecond}
      />
      <SettingsButton
        onClick={this.handleSubmit}
        text={this.props.buttonLabel}
        enabled={this.isValid()}
        error={errorText}
      />
   </div>;
  }
}
